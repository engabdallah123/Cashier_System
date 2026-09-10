using Dapper;
using POS.Shared.Application.Database;
using POS.Shared.Application.IService;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using System.Data;

namespace Dashboard.Application.Dashboard.Queries.GetDashboard
{
    internal sealed class GetDashboardQueryHandler : IQueryHandler<GetDashboardQuery, DashboardResponse>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;
        private readonly ICacheService _cacheService;

        public GetDashboardQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory,
            ICacheService cacheService)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
            _cacheService = cacheService;
        }

        public async Task<Result<DashboardResponse>> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
        {
            var cacheKey = $"dashboard_{request.FromDate?.Ticks ?? 0}_{request.ToDate?.Ticks ?? 0}";

            var cachedResponse = await _cacheService.GetAsync<DashboardResponse>(cacheKey, cancellationToken);
            if (cachedResponse is not null)
            {
                return Result<DashboardResponse>.Success(cachedResponse);
            }

            using var connection = _sqlConnectionFactory.CreateConnection();

            DateTime fromDate = request.FromDate.HasValue
                ? (request.FromDate.Value.Kind == DateTimeKind.Utc ? request.FromDate.Value : DateTime.SpecifyKind(request.FromDate.Value, DateTimeKind.Local).ToUniversalTime())
                : new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            DateTime toDate = request.ToDate.HasValue
                ? (request.ToDate.Value.Kind == DateTimeKind.Utc ? request.ToDate.Value : DateTime.SpecifyKind(request.ToDate.Value, DateTimeKind.Local).ToUniversalTime())
                : DateTime.UtcNow.AddDays(1);

            const string salesSql = """
                SELECT 
                    ISNULL(SUM(s.TotalAmount), 0) AS TotalSales,
                    ISNULL(SUM(
                        CASE 
                            WHEN s.PaidAmount <= 0 THEN 0
                            WHEN s.PaidAmount - ISNULL(col.TotalCollected, 0) < 0 THEN 0
                            ELSE s.PaidAmount - ISNULL(col.TotalCollected, 0)
                        END
                    ), 0) AS CashPaidAtSale,
                    ISNULL(SUM(
                        s.TotalAmount - (
                            CASE 
                                WHEN s.PaidAmount <= 0 THEN 0
                                WHEN s.PaidAmount - ISNULL(col.TotalCollected, 0) < 0 THEN 0
                                ELSE s.PaidAmount - ISNULL(col.TotalCollected, 0)
                            END
                        )
                    ), 0) AS CreditSales,
                    COUNT(1) AS TotalInvoices
                FROM [Sales].[Sales] s
                OUTER APPLY (
                    SELECT SUM(p.Amount) AS TotalCollected
                    FROM [Sales].[SalePayments] p
                    WHERE p.SaleId = s.Id 
                      AND (p.Notes LIKE N'%تحصيل%' OR p.PaymentDate > DATEADD(minute, 5, s.SaleDate))
                ) col
                WHERE s.Status IN (1, 4) AND s.SaleDate >= @FromDate AND s.SaleDate <= @ToDate
                """;

            var salesMetrics = await connection.QuerySingleAsync(salesSql, new { FromDate = fromDate, ToDate = toDate });
            decimal totalSales = Convert.ToDecimal(salesMetrics.TotalSales);
            decimal cashSalesAmount = Convert.ToDecimal(salesMetrics.CashPaidAtSale);
            decimal creditSalesAmount = Convert.ToDecimal(salesMetrics.CreditSales);
            int totalInvoices = Convert.ToInt32(salesMetrics.TotalInvoices);

            // استعلام تحصيلات الديون السابقة خلال الفترة
            const string collectionsSql = """
                IF EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'SalePayments' AND s.name = 'Sales')
                    SELECT ISNULL(SUM(p.Amount), 0)
                    FROM [Sales].[SalePayments] p
                    JOIN [Sales].[Sales] s ON p.SaleId = s.Id
                    WHERE p.PaymentDate >= @FromDate AND p.PaymentDate <= @ToDate
                      AND (p.Notes LIKE N'%تحصيل%' OR p.PaymentDate > DATEADD(minute, 5, s.SaleDate))
                ELSE
                    SELECT CAST(0 AS decimal(18,2))
                """;
            decimal debtCollectionsAmount = await connection.QuerySingleAsync<decimal>(collectionsSql, new { FromDate = fromDate, ToDate = toDate });

            const string purchasesSql = """
                SELECT ISNULL(SUM(TotalAmount), 0) 
                FROM [Purchases].[Purchases]
                WHERE Status = 2 AND PurchaseDate >= @FromDate AND PurchaseDate <= @ToDate
                """;
            decimal totalPurchases = await connection.QuerySingleAsync<decimal>(purchasesSql, new { FromDate = fromDate, ToDate = toDate });

            const string expensesSql = """
                SELECT ISNULL(SUM(Amount), 0) 
                FROM [Expenses].[Expenses]
                WHERE ExpenseDate >= @FromDate AND ExpenseDate <= @ToDate
                """;
            decimal totalExpenses = await connection.QuerySingleAsync<decimal>(expensesSql, new { FromDate = fromDate, ToDate = toDate });

            const string salesReturnsSql = """
                SELECT ISNULL(SUM(TotalAmount), 0) 
                FROM [Returns].[SalesReturns]
                WHERE Status = 1 AND ReturnDate >= @FromDate AND ReturnDate <= @ToDate
                """;
            decimal totalSalesReturns = await connection.QuerySingleAsync<decimal>(salesReturnsSql, new { FromDate = fromDate, ToDate = toDate });

            const string purchaseReturnsSql = """
                SELECT ISNULL(SUM(TotalAmount), 0) 
                FROM [Returns].[PurchaseReturns]
                WHERE Status = 1 AND ReturnDate >= @FromDate AND ReturnDate <= @ToDate
                """;
            decimal totalPurchaseReturns = await connection.QuerySingleAsync<decimal>(purchaseReturnsSql, new { FromDate = fromDate, ToDate = toDate });

            const string lowStockSql = """
                SELECT COUNT(1) 
                FROM [Inventory].[Products]
                WHERE IsActive = 1 AND QuantityInStock <= ReorderLevel
                """;
            int lowStockCount = await connection.QuerySingleAsync<int>(lowStockSql);

            const string lowStockListSql = """
                SELECT TOP 10 
                    Id AS ProductId,
                    NameAr AS ProductName,
                    Barcode,
                    QuantityInStock,
                    ReorderLevel
                FROM [Inventory].[Products]
                WHERE IsActive = 1 AND QuantityInStock <= ReorderLevel
                ORDER BY QuantityInStock ASC
                """;
            var lowStockList = (await connection.QueryAsync<LowStockProductResponse>(lowStockListSql)).ToList();

            const string topProductsSql = """
                SELECT TOP 100
                    i.ProductId,
                    p.NameAr AS ProductName,
                    p.Barcode,
                    SUM(i.Quantity) AS TotalQuantitySold,
                    SUM(i.Total) AS TotalRevenue
                FROM [Sales].[SaleItems] i
                JOIN [Sales].[Sales] s ON i.SaleId = s.Id
                LEFT JOIN [Inventory].[Products] p ON i.ProductId = p.Id
                WHERE s.Status IN (1, 4) AND s.SaleDate >= @FromDate AND s.SaleDate <= @ToDate
                GROUP BY i.ProductId, p.NameAr, p.Barcode
                ORDER BY TotalQuantitySold DESC, TotalRevenue DESC
                """;
            var topProducts = (await connection.QueryAsync<TopProductResponse>(topProductsSql, new { FromDate = fromDate, ToDate = toDate })).ToList();

            const string cashierSql = """
                SELECT 
                    s.CashierId,
                    ISNULL(u.FullName, 'Cashier') AS CashierName,
                    COUNT(DISTINCT s.Id) AS TotalShifts,
                    ISNULL(SUM(s.TotalInvoices), 0) AS TotalInvoices,
                    ISNULL(SUM(s.TotalSales), 0) AS TotalSalesAmount,
                    ISNULL(SUM(s.CashDifference), 0) AS TotalCashDifference
                FROM [Shifts].[Shifts] s
                LEFT JOIN [Identity].[AspNetUsers] u ON s.CashierId = TRY_CAST(u.Id AS uniqueidentifier)
                WHERE s.OpenedAt >= @FromDate AND s.OpenedAt <= @ToDate
                GROUP BY s.CashierId, u.FullName
                """;
            var cashierPerformances = (await connection.QueryAsync<CashierPerformanceResponse>(cashierSql, new { FromDate = fromDate, ToDate = toDate })).ToList();

            const string paymentMethodsSql = """
                SELECT 
                    PaymentMethod,
                    ISNULL(SUM(TotalAmount), 0) AS TotalAmount,
                    COUNT(1) AS InvoiceCount
                FROM [Sales].[Sales]
                WHERE Status IN (1, 4) AND SaleDate >= @FromDate AND SaleDate <= @ToDate
                GROUP BY PaymentMethod
                """;
            var rawPaymentMethods = (await connection.QueryAsync(paymentMethodsSql, new { FromDate = fromDate, ToDate = toDate })).ToList();
            var paymentMethodsSummary = rawPaymentMethods.Select(p => {
                string method = Convert.ToString(p.PaymentMethod) ?? "Cash";
                decimal amount = Convert.ToDecimal(p.TotalAmount);
                int count = Convert.ToInt32(p.InvoiceCount);
                decimal pct = totalSales > 0 ? (amount / totalSales) * 100 : 0;
                return new PaymentMethodSummaryResponse(method, amount, count, Math.Round(pct, 1));
            }).ToList();

            // Customer Debts (الديون المستحقة عند العملاء / اللي ليا بره)
            const string customerDebtsSql = """
                SELECT 
                    ISNULL(SUM(TotalAmount - PaidAmount), 0) AS TotalCustomerDebts,
                    COUNT(1) AS CustomerDebtsCount
                FROM [Sales].[Sales]
                WHERE Status = 1 AND (TotalAmount - PaidAmount) > 0.001
                """;
            var customerDebtsRaw = await connection.QuerySingleAsync(customerDebtsSql);
            decimal totalCustomerDebts = Convert.ToDecimal(customerDebtsRaw.TotalCustomerDebts);
            int customerDebtsCount = Convert.ToInt32(customerDebtsRaw.CustomerDebtsCount);

            const string topDebtsSql = """
                SELECT TOP 5
                    s.CustomerId,
                    ISNULL(c.Name, N'عميل غير مسجل') AS CustomerName,
                    c.Phone AS CustomerPhone,
                    SUM(s.TotalAmount - s.PaidAmount) AS TotalDebtAmount,
                    COUNT(1) AS InvoicesCount,
                    MAX(s.SaleDate) AS LastSaleDate
                FROM [Sales].[Sales] s
                LEFT JOIN [Sales].[Customers] c ON s.CustomerId = c.Id
                WHERE s.Status = 1 AND (s.TotalAmount - s.PaidAmount) > 0.001
                GROUP BY s.CustomerId, c.Name, c.Phone
                ORDER BY TotalDebtAmount DESC
                """;
            var topCustomerDebts = (await connection.QueryAsync<CustomerDebtSummaryResponse>(topDebtsSql)).ToList();

            var nowLocal = DateTime.Now;
            var todayStart = DateTime.SpecifyKind(nowLocal.Date, DateTimeKind.Local).ToUniversalTime();
            var todayEnd = DateTime.SpecifyKind(nowLocal.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime();

            var monthStart = DateTime.SpecifyKind(new DateTime(nowLocal.Year, nowLocal.Month, 1), DateTimeKind.Local).ToUniversalTime();
            var monthEnd = DateTime.SpecifyKind(new DateTime(nowLocal.Year, nowLocal.Month, 1).AddMonths(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime();

            var yearStart = DateTime.SpecifyKind(new DateTime(nowLocal.Year, 1, 1), DateTimeKind.Local).ToUniversalTime();
            var yearEnd = DateTime.SpecifyKind(new DateTime(nowLocal.Year, 1, 1).AddYears(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime();

            var todayMetrics = await GetPeriodMetricsAsync(connection, todayStart, todayEnd);
            var monthMetrics = await GetPeriodMetricsAsync(connection, monthStart, monthEnd);
            var yearMetrics = await GetPeriodMetricsAsync(connection, yearStart, yearEnd);

            decimal netSales = Math.Max(0, totalSales - totalSalesReturns);
            decimal totalCashCollected = cashSalesAmount + debtCollectionsAmount;
            decimal realizedRevenue = Math.Max(0, totalCashCollected - totalSalesReturns);
            decimal netProfit = realizedRevenue - (totalPurchases - totalPurchaseReturns) - totalExpenses;
            decimal avgInvoiceValue = totalInvoices > 0 ? netSales / totalInvoices : 0;
            decimal profitMarginPct = realizedRevenue > 0 ? (netProfit / realizedRevenue) * 100 : 0;

            // إحصائيات الخسائر والهالك بسعر التكلفة (شراء)
            var weekStart = todayStart.AddDays(-7);
            const string wasteSql = """
                SELECT 
                    ISNULL(SUM(CASE WHEN CreatedAt >= @TodayStart THEN TotalCost ELSE 0 END), 0) AS TodayLoss,
                    ISNULL(SUM(CASE WHEN CreatedAt >= @WeekStart THEN TotalCost ELSE 0 END), 0) AS WeekLoss,
                    ISNULL(SUM(CASE WHEN CreatedAt >= @MonthStart THEN TotalCost ELSE 0 END), 0) AS MonthLoss,
                    ISNULL(SUM(TotalCost), 0) AS TotalLoss
                FROM [Inventory].[InventoryWastes]
                """;

            var wasteStats = await connection.QuerySingleAsync(wasteSql, new {
                TodayStart = todayStart,
                WeekStart = weekStart,
                MonthStart = monthStart
            });

            var wasteLosses = new WasteLossesResponse(
                Convert.ToDecimal(wasteStats.TodayLoss),
                Convert.ToDecimal(wasteStats.WeekLoss),
                Convert.ToDecimal(wasteStats.MonthLoss),
                Convert.ToDecimal(wasteStats.TotalLoss));

            const string notifsCountSql = """
                SELECT COUNT(1)
                FROM [Inventory].[ExpiryNotifications]
                WHERE Status = 'Active' OR (Status = 'Snoozed' AND SnoozedUntil <= GETUTCDATE())
                """;
            int activeNotifsCount = await connection.QuerySingleAsync<int>(notifsCountSql);

            var dashboard = new DashboardResponse(
                totalSales,
                totalInvoices,
                totalPurchases,
                totalExpenses,
                netProfit,
                totalSalesReturns,
                totalPurchaseReturns,
                avgInvoiceValue,
                profitMarginPct,
                lowStockCount,
                todayMetrics,
                monthMetrics,
                yearMetrics,
                topProducts,
                cashierPerformances,
                paymentMethodsSummary,
                lowStockList,
                totalCustomerDebts,
                customerDebtsCount,
                topCustomerDebts,
                wasteLosses,
                activeNotifsCount,
                cashSalesAmount,
                creditSalesAmount,
                debtCollectionsAmount,
                realizedRevenue);

            await _cacheService.SetAsync(
                cacheKey,
                dashboard,
                absoluteExpiration: TimeSpan.FromMinutes(2),
                slidingExpiration: TimeSpan.FromMinutes(1),
                ct: cancellationToken);

            return Result<DashboardResponse>.Success(dashboard);
        }

        private static async Task<PeriodMetricsResponse> GetPeriodMetricsAsync(IDbConnection connection, DateTime start, DateTime end)
        {
            const string periodSql = """
                SELECT 
                    (SELECT ISNULL(SUM(TotalAmount), 0) FROM [Sales].[Sales] WHERE Status IN (1, 4) AND SaleDate >= @Start AND SaleDate <= @End) AS TotalSales,
                    (SELECT ISNULL(SUM(
                        CASE 
                            WHEN s.PaidAmount <= 0 THEN 0
                            WHEN s.PaidAmount - ISNULL(col.TotalCollected, 0) < 0 THEN 0
                            ELSE s.PaidAmount - ISNULL(col.TotalCollected, 0)
                        END
                    ), 0) 
                    FROM [Sales].[Sales] s 
                    OUTER APPLY (
                        SELECT SUM(p.Amount) AS TotalCollected
                        FROM [Sales].[SalePayments] p
                        WHERE p.SaleId = s.Id 
                          AND (p.Notes LIKE N'%تحصيل%' OR p.PaymentDate > DATEADD(minute, 5, s.SaleDate))
                    ) col
                    WHERE s.Status IN (1, 4) AND s.SaleDate >= @Start AND s.SaleDate <= @End) AS CashPaidAtSale,
                    (SELECT ISNULL(SUM(
                        s.TotalAmount - (
                            CASE 
                                WHEN s.PaidAmount <= 0 THEN 0
                                WHEN s.PaidAmount - ISNULL(col.TotalCollected, 0) < 0 THEN 0
                                ELSE s.PaidAmount - ISNULL(col.TotalCollected, 0)
                            END
                        )
                    ), 0) 
                    FROM [Sales].[Sales] s 
                    OUTER APPLY (
                        SELECT SUM(p.Amount) AS TotalCollected
                        FROM [Sales].[SalePayments] p
                        WHERE p.SaleId = s.Id 
                          AND (p.Notes LIKE N'%تحصيل%' OR p.PaymentDate > DATEADD(minute, 5, s.SaleDate))
                    ) col
                    WHERE s.Status IN (1, 4) AND s.SaleDate >= @Start AND s.SaleDate <= @End) AS CreditSales,
                    (SELECT COUNT(1) FROM [Sales].[Sales] WHERE Status IN (1, 4) AND SaleDate >= @Start AND SaleDate <= @End) AS TotalInvoices,
                    (CASE WHEN EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = 'SalePayments' AND s.name = 'Sales')
                          THEN (SELECT ISNULL(SUM(p.Amount), 0) FROM [Sales].[SalePayments] p JOIN [Sales].[Sales] s ON p.SaleId = s.Id WHERE p.PaymentDate >= @Start AND p.PaymentDate <= @End AND (p.Notes LIKE N'%تحصيل%' OR p.PaymentDate > DATEADD(minute, 5, s.SaleDate)))
                          ELSE 0 END) AS DebtCollections,
                    (SELECT ISNULL(SUM(TotalAmount), 0) FROM [Purchases].[Purchases] WHERE Status = 2 AND PurchaseDate >= @Start AND PurchaseDate <= @End) AS TotalPurchases,
                    (SELECT ISNULL(SUM(Amount), 0) FROM [Expenses].[Expenses] WHERE ExpenseDate >= @Start AND ExpenseDate <= @End) AS TotalExpenses,
                    (SELECT ISNULL(SUM(TotalAmount), 0) FROM [Returns].[SalesReturns] WHERE Status = 1 AND ReturnDate >= @Start AND ReturnDate <= @End) AS SalesReturns,
                    (SELECT ISNULL(SUM(TotalAmount), 0) FROM [Returns].[PurchaseReturns] WHERE Status = 1 AND ReturnDate >= @Start AND ReturnDate <= @End) AS PurchaseReturns
                """;

            var raw = await connection.QuerySingleAsync(periodSql, new { Start = start, End = end });
            decimal grossSales = Convert.ToDecimal(raw.TotalSales);
            decimal cashSales = Convert.ToDecimal(raw.CashPaidAtSale);
            decimal creditSales = Convert.ToDecimal(raw.CreditSales);
            decimal debtCollections = Convert.ToDecimal(raw.DebtCollections);
            int invoices = Convert.ToInt32(raw.TotalInvoices);
            decimal purchases = Convert.ToDecimal(raw.TotalPurchases);
            decimal expenses = Convert.ToDecimal(raw.TotalExpenses);
            decimal salesReturns = Convert.ToDecimal(raw.SalesReturns);
            decimal purchaseReturns = Convert.ToDecimal(raw.PurchaseReturns);

            decimal realizedRevenue = Math.Max(0, (cashSales + debtCollections) - salesReturns);
            decimal netProfit = realizedRevenue - (purchases - purchaseReturns) - expenses;

            return new PeriodMetricsResponse(grossSales, netProfit, invoices, purchases, expenses, cashSales, creditSales, debtCollections, realizedRevenue);
        }
    }
}
