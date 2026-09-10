using Dapper;
using POS.Shared.Application.Database;
using POS.Shared.Application.IService;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using System.Globalization;

namespace Dashboard.Application.Dashboard.Queries.GetMonthlySalesCalendar
{
    internal sealed class GetMonthlySalesCalendarQueryHandler : IQueryHandler<GetMonthlySalesCalendarQuery, MonthlySalesCalendarResponse>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;
        private readonly ICacheService _cacheService;

        private static readonly string[] ArabicMonthNames = new[]
        {
            "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
            "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
        };

        private static readonly string[] ArabicDayNames = new[]
        {
            "السبت", "الأحد", "الإثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة"
        };

        public GetMonthlySalesCalendarQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory,
            ICacheService cacheService)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
            _cacheService = cacheService;
        }

        public async Task<Result<MonthlySalesCalendarResponse>> Handle(GetMonthlySalesCalendarQuery request, CancellationToken cancellationToken)
        {
            int year = request.Year > 2000 ? request.Year : DateTime.Today.Year;
            int month = request.Month >= 1 && request.Month <= 12 ? request.Month : DateTime.Today.Month;
            string paymentMethod = request.PaymentMethod?.Trim() ?? string.Empty;
            Guid? cashierId = request.CashierId;

            // Only check cache for past months (never cache the current active month/year)
            bool isCurrentMonth = (year == DateTime.Today.Year && month == DateTime.Today.Month);
            var cacheKey = $"monthly_calendar_{year}_{month}_{paymentMethod}_{cashierId?.ToString() ?? "all"}";
            if (!isCurrentMonth)
            {
                var cached = await _cacheService.GetAsync<MonthlySalesCalendarResponse>(cacheKey, cancellationToken);
                if (cached is not null)
                {
                    return Result<MonthlySalesCalendarResponse>.Success(cached);
                }
            }

            int daysInMonth = DateTime.DaysInMonth(year, month);
            var firstDayDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Local);
            var lastDayDate = new DateTime(year, month, daysInMonth, 23, 59, 59, 999, DateTimeKind.Local);

            var startUtc = firstDayDate.ToUniversalTime();
            var endUtc = lastDayDate.ToUniversalTime();
            int utcOffsetMinutes = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes;

            // Arabic Day of Week: Saturday = 0, Sunday = 1, ..., Friday = 6
            int firstDayOfWeekIndex = ((int)firstDayDate.DayOfWeek + 1) % 7;
            string monthNameAr = ArabicMonthNames[month - 1];

            using var connection = _sqlConnectionFactory.CreateConnection();

            // 1. Daily Sales (Convert UTC to Local Time before grouping by DAY)
            const string salesSql = """
                SELECT 
                    DAY(DATEADD(minute, @UtcOffsetMinutes, s.SaleDate)) AS [Day],
                    ISNULL(SUM(s.TotalAmount), 0) AS TotalSales,
                    ISNULL(SUM(
                        CASE 
                            WHEN s.PaidAmount <= 0 THEN 0
                            WHEN s.PaidAmount - ISNULL(col.TotalCollected, 0) < 0 THEN 0
                            ELSE s.PaidAmount - ISNULL(col.TotalCollected, 0)
                        END
                    ), 0) AS TotalPaid,
                    ISNULL(SUM(
                        s.TotalAmount - (
                            CASE 
                                WHEN s.PaidAmount <= 0 THEN 0
                                WHEN s.PaidAmount - ISNULL(col.TotalCollected, 0) < 0 THEN 0
                                ELSE s.PaidAmount - ISNULL(col.TotalCollected, 0)
                            END
                        )
                    ), 0) AS TotalCredit,
                    COUNT(1) AS InvoiceCount
                FROM [Sales].[Sales] s
                OUTER APPLY (
                    SELECT SUM(p.Amount) AS TotalCollected
                    FROM [Sales].[SalePayments] p
                    WHERE p.SaleId = s.Id 
                      AND (p.Notes LIKE N'%تحصيل%' OR p.PaymentDate > DATEADD(minute, 5, s.SaleDate))
                ) col
                WHERE s.Status IN (1, 4)
                  AND s.SaleDate >= @StartDate AND s.SaleDate <= @EndDate
                  AND (@PaymentMethod = '' OR @PaymentMethod = 'All' OR s.PaymentMethod = @PaymentMethod)
                  AND (@CashierId IS NULL OR s.CashierId = @CashierId)
                GROUP BY DAY(DATEADD(minute, @UtcOffsetMinutes, s.SaleDate))
                """;

            var salesData = (await connection.QueryAsync(salesSql, new
            {
                StartDate = startUtc,
                EndDate = endUtc,
                UtcOffsetMinutes = utcOffsetMinutes,
                PaymentMethod = paymentMethod,
                CashierId = cashierId
            })).ToDictionary(x => (int)x.Day, x => new
            {
                TotalSales = Convert.ToDecimal(x.TotalSales),
                TotalPaid = Convert.ToDecimal(x.TotalPaid),
                TotalCredit = Convert.ToDecimal(x.TotalCredit),
                InvoiceCount = Convert.ToInt32(x.InvoiceCount)
            });

            // 1.1 Daily Debt Collections
            const string debtCollectionsSql = """
                IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'SalePayments' AND s.name = 'Sales')
                    SELECT 
                        DAY(DATEADD(minute, @UtcOffsetMinutes, p.PaymentDate)) AS [Day],
                        ISNULL(SUM(p.Amount), 0) AS TotalCollections
                    FROM [Sales].[SalePayments] p
                    JOIN [Sales].[Sales] s ON p.SaleId = s.Id
                    WHERE p.PaymentDate >= @StartDate AND p.PaymentDate <= @EndDate
                      AND (p.Notes LIKE N'%تحصيل%' OR p.PaymentDate > DATEADD(minute, 5, s.SaleDate))
                      AND (@CashierId IS NULL OR p.CashierId = @CashierId)
                    GROUP BY DAY(DATEADD(minute, @UtcOffsetMinutes, p.PaymentDate))
                ELSE
                    SELECT 1 AS [Day], CAST(0 AS decimal(18,2)) AS TotalCollections WHERE 1=0
                """;

            var debtCollectionsData = (await connection.QueryAsync(debtCollectionsSql, new
            {
                StartDate = startUtc,
                EndDate = endUtc,
                UtcOffsetMinutes = utcOffsetMinutes,
                CashierId = cashierId
            })).ToDictionary(x => (int)x.Day, x => Convert.ToDecimal(x.TotalCollections));

            // 2. Daily Sales Returns
            const string returnsSql = """
                SELECT 
                    DAY(DATEADD(minute, @UtcOffsetMinutes, ReturnDate)) AS [Day],
                    ISNULL(SUM(TotalAmount), 0) AS TotalReturns
                FROM [Returns].[SalesReturns]
                WHERE Status = 1
                  AND ReturnDate >= @StartDate AND ReturnDate <= @EndDate
                  AND (@CashierId IS NULL OR CashierId = @CashierId)
                GROUP BY DAY(DATEADD(minute, @UtcOffsetMinutes, ReturnDate))
                """;

            var returnsData = (await connection.QueryAsync(returnsSql, new
            {
                StartDate = startUtc,
                EndDate = endUtc,
                UtcOffsetMinutes = utcOffsetMinutes,
                CashierId = cashierId
            })).ToDictionary(x => (int)x.Day, x => Convert.ToDecimal(x.TotalReturns));

            // 3. Daily Expenses
            const string expensesSql = """
                SELECT 
                    DAY(DATEADD(minute, @UtcOffsetMinutes, ExpenseDate)) AS [Day],
                    ISNULL(SUM(Amount), 0) AS TotalExpenses
                FROM [Expenses].[Expenses]
                WHERE ExpenseDate >= @StartDate AND ExpenseDate <= @EndDate
                GROUP BY DAY(DATEADD(minute, @UtcOffsetMinutes, ExpenseDate))
                """;

            var expensesData = (await connection.QueryAsync(expensesSql, new
            {
                StartDate = startUtc,
                EndDate = endUtc,
                UtcOffsetMinutes = utcOffsetMinutes
            })).ToDictionary(x => (int)x.Day, x => Convert.ToDecimal(x.TotalExpenses));

            // 4. Daily Purchases
            const string purchasesSql = """
                SELECT 
                    DAY(DATEADD(minute, @UtcOffsetMinutes, PurchaseDate)) AS [Day],
                    ISNULL(SUM(TotalAmount), 0) AS TotalPurchases
                FROM [Purchases].[Purchases]
                WHERE Status = 2
                  AND PurchaseDate >= @StartDate AND PurchaseDate <= @EndDate
                GROUP BY DAY(DATEADD(minute, @UtcOffsetMinutes, PurchaseDate))
                """;

            var purchasesData = (await connection.QueryAsync(purchasesSql, new
            {
                StartDate = startUtc,
                EndDate = endUtc,
                UtcOffsetMinutes = utcOffsetMinutes
            })).ToDictionary(x => (int)x.Day, x => Convert.ToDecimal(x.TotalPurchases));

            // Build Days List
            var daysList = new List<MonthlyDaySalesDto>(daysInMonth);
            var today = DateTime.Today;

            decimal grossMonthlySales = 0;
            decimal totalMonthlyCashSales = 0;
            decimal totalMonthlyCreditSales = 0;
            decimal totalMonthlyDebtCollections = 0;
            int totalMonthlyInvoices = 0;
            decimal totalMonthlyReturns = 0;
            decimal totalMonthlyExpenses = 0;
            decimal totalMonthlyPurchases = 0;

            int activeDaysCount = 0;
            int highestSalesDay = 0;
            decimal highestSalesAmount = 0;
            int lowestSalesDay = 0;
            decimal lowestSalesAmount = decimal.MaxValue;

            for (int day = 1; day <= daysInMonth; day++)
            {
                var currentDate = new DateTime(year, month, day);
                int dayOfWeekIndex = ((int)currentDate.DayOfWeek + 1) % 7;
                string dayNameAr = ArabicDayNames[dayOfWeekIndex];

                decimal totalSales = salesData.TryGetValue(day, out var s) ? s.TotalSales : 0;
                int invoiceCount = s?.InvoiceCount ?? 0;
                decimal cashSales = s?.TotalPaid ?? 0;
                decimal creditSales = s?.TotalCredit ?? 0;
                decimal debtCollections = debtCollectionsData.TryGetValue(day, out var col) ? col : 0;
                decimal totalPaid = cashSales + debtCollections;
                decimal totalReturns = returnsData.TryGetValue(day, out var r) ? r : 0;
                decimal totalExpenses = expensesData.TryGetValue(day, out var exp) ? exp : 0;
                decimal totalPurchases = purchasesData.TryGetValue(day, out var pur) ? pur : 0;
                decimal netSales = Math.Max(0, totalSales - totalReturns);
                bool hasSales = totalSales > 0 || invoiceCount > 0 || totalReturns > 0 || debtCollections > 0;
                bool isToday = (currentDate.Year == today.Year && currentDate.Month == today.Month && currentDate.Day == today.Day);
                bool isWeekend = (currentDate.DayOfWeek == DayOfWeek.Friday);

                if (netSales > 0)
                {
                    activeDaysCount++;
                    if (netSales > highestSalesAmount)
                    {
                        highestSalesAmount = netSales;
                        highestSalesDay = day;
                    }
                    if (netSales < lowestSalesAmount)
                    {
                        lowestSalesAmount = netSales;
                        lowestSalesDay = day;
                    }
                }

                grossMonthlySales += totalSales;
                totalMonthlyCashSales += cashSales;
                totalMonthlyCreditSales += creditSales;
                totalMonthlyDebtCollections += debtCollections;
                totalMonthlyInvoices += invoiceCount;
                totalMonthlyReturns += totalReturns;
                totalMonthlyExpenses += totalExpenses;
                totalMonthlyPurchases += totalPurchases;

                daysList.Add(new MonthlyDaySalesDto(
                    DayNumber: day,
                    Date: currentDate,
                    DayOfWeekIndex: dayOfWeekIndex,
                    DayNameAr: dayNameAr,
                    TotalSales: totalSales,
                    InvoiceCount: invoiceCount,
                    TotalReturns: totalReturns,
                    NetSales: netSales,
                    TotalPaid: totalPaid,
                    TotalExpenses: totalExpenses,
                    TotalPurchases: totalPurchases,
                    HasSales: hasSales,
                    IsToday: isToday,
                    IsWeekend: isWeekend,
                    CashSales: cashSales,
                    CreditSales: creditSales,
                    DebtCollections: debtCollections));
            }

            if (lowestSalesAmount == decimal.MaxValue)
            {
                lowestSalesAmount = 0;
                lowestSalesDay = 0;
            }

            decimal netMonthlySales = Math.Max(0, grossMonthlySales - totalMonthlyReturns);
            decimal dailyAverageSales = daysInMonth > 0 ? Math.Round(netMonthlySales / daysInMonth, 2) : 0;
            decimal dailyAverageActiveDays = activeDaysCount > 0 ? Math.Round(netMonthlySales / activeDaysCount, 2) : 0;
            decimal totalMonthlyCollected = totalMonthlyCashSales + totalMonthlyDebtCollections;

            var response = new MonthlySalesCalendarResponse(
                Year: year,
                Month: month,
                MonthNameAr: monthNameAr,
                DaysInMonth: daysInMonth,
                FirstDayDayOfWeek: firstDayOfWeekIndex,
                TotalMonthlySales: netMonthlySales,
                TotalMonthlyInvoices: totalMonthlyInvoices,
                TotalMonthlyReturns: totalMonthlyReturns,
                NetMonthlySales: netMonthlySales,
                TotalMonthlyExpenses: totalMonthlyExpenses,
                TotalMonthlyPurchases: totalMonthlyPurchases,
                DailyAverageSales: dailyAverageSales,
                DailyAverageSalesActiveDays: dailyAverageActiveDays,
                ActiveDaysCount: activeDaysCount,
                HighestSalesDay: highestSalesDay,
                HighestSalesAmount: highestSalesAmount,
                LowestSalesDay: lowestSalesDay,
                LowestSalesAmount: lowestSalesAmount,
                Days: daysList,
                TotalMonthlyCashSales: totalMonthlyCashSales,
                TotalMonthlyCreditSales: totalMonthlyCreditSales,
                TotalMonthlyDebtCollections: totalMonthlyDebtCollections,
                TotalMonthlyCollected: totalMonthlyCollected);

            if (!isCurrentMonth)
            {
                await _cacheService.SetAsync(
                    cacheKey,
                    response,
                    absoluteExpiration: TimeSpan.FromMinutes(30),
                    slidingExpiration: TimeSpan.FromMinutes(10),
                    ct: cancellationToken);
            }

            return Result<MonthlySalesCalendarResponse>.Success(response);
        }
    }
}
