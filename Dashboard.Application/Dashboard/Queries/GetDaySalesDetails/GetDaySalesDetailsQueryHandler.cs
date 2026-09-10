using Dapper;
using POS.Shared.Application.Database;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Dashboard.Application.Dashboard.Queries.GetDaySalesDetails
{
    internal sealed class GetDaySalesDetailsQueryHandler : IQueryHandler<GetDaySalesDetailsQuery, DaySalesDetailsResponse>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        private static readonly string[] ArabicDayNames = new[]
        {
            "السبت", "الأحد", "الإثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة"
        };

        public GetDaySalesDetailsQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<Result<DaySalesDetailsResponse>> Handle(GetDaySalesDetailsQuery request, CancellationToken cancellationToken)
        {
            var localDate = request.Date.Date;
            var startUtc = DateTime.SpecifyKind(localDate, DateTimeKind.Local).ToUniversalTime();
            var endUtc = DateTime.SpecifyKind(localDate.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime();

            int dayOfWeekIndex = ((int)localDate.DayOfWeek + 1) % 7;
            string dayNameAr = ArabicDayNames[dayOfWeekIndex];

            string paymentMethod = request.PaymentMethod?.Trim() ?? string.Empty;
            Guid? cashierId = request.CashierId;

            using var connection = _sqlConnectionFactory.CreateConnection();

            const string invoicesSql = """
                SELECT 
                    s.Id,
                    s.InvoiceNumber,
                    s.SaleDate,
                    s.CashierId,
                    ISNULL(u.FullName, N'كاشير') AS CashierName,
                    s.CustomerId,
                    ISNULL(c.Name, N'عميل نقدي') AS CustomerName,
                    s.SubTotal,
                    s.DiscountAmount,
                    s.TaxAmount,
                    s.TotalAmount,
                    s.PaidAmount,
                    s.PaymentMethod,
                    s.Status,
                    s.Notes,
                    (SELECT COUNT(1) FROM [Sales].[SaleItems] WHERE SaleId = s.Id) AS ItemCount
                FROM [Sales].[Sales] s
                LEFT JOIN [Sales].[Customers] c ON s.CustomerId = c.Id
                LEFT JOIN [Identity].[AspNetUsers] u ON s.CashierId = TRY_CAST(u.Id AS uniqueidentifier)
                WHERE s.SaleDate >= @StartDate AND s.SaleDate <= @EndDate
                  AND (@PaymentMethod = '' OR @PaymentMethod = 'All' OR s.PaymentMethod = @PaymentMethod)
                  AND (@CashierId IS NULL OR s.CashierId = @CashierId)
                ORDER BY s.SaleDate DESC
                """;

            var invoices = (await connection.QueryAsync<DayInvoiceSummaryDto>(invoicesSql, new
            {
                StartDate = startUtc,
                EndDate = endUtc,
                PaymentMethod = paymentMethod,
                CashierId = cashierId
            })).ToList();

            const string returnsSql = """
                SELECT ISNULL(SUM(TotalAmount), 0)
                FROM [Returns].[SalesReturns]
                WHERE Status = 1 AND ReturnDate >= @StartDate AND ReturnDate <= @EndDate
                """;
            decimal totalReturns = await connection.QuerySingleAsync<decimal>(returnsSql, new { StartDate = startUtc, EndDate = endUtc });

            const string expensesSql = """
                SELECT ISNULL(SUM(Amount), 0)
                FROM [Expenses].[Expenses]
                WHERE ExpenseDate >= @StartDate AND ExpenseDate <= @EndDate
                """;
            decimal totalExpenses = await connection.QuerySingleAsync<decimal>(expensesSql, new { StartDate = startUtc, EndDate = endUtc });

            const string purchasesSql = """
                SELECT ISNULL(SUM(TotalAmount), 0)
                FROM [Purchases].[Purchases]
                WHERE Status = 2 AND PurchaseDate >= @StartDate AND PurchaseDate <= @EndDate
                """;
            decimal totalPurchases = await connection.QuerySingleAsync<decimal>(purchasesSql, new { StartDate = startUtc, EndDate = endUtc });

            decimal grossSales = invoices.Where(i => i.Status == 1 || i.Status == 4).Sum(i => i.TotalAmount);
            decimal netSales = Math.Max(0, grossSales - totalReturns);
            int totalInvoicesCount = invoices.Count;

            var response = new DaySalesDetailsResponse(
                Date: localDate,
                DayNameAr: dayNameAr,
                TotalSales: netSales,
                TotalInvoices: totalInvoicesCount,
                TotalReturns: totalReturns,
                TotalExpenses: totalExpenses,
                TotalPurchases: totalPurchases,
                Invoices: invoices);

            return Result<DaySalesDetailsResponse>.Success(response);
        }
    }
}
