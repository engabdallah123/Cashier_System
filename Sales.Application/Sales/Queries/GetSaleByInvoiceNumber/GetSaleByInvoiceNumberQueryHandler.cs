using Dapper;
using POS.Shared.Application.Database;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Sales.Application.Sales.Queries.GetSaleById;

namespace Sales.Application.Sales.Queries.GetSaleByInvoiceNumber
{
    internal sealed class GetSaleByInvoiceNumberQueryHandler : IQueryHandler<GetSaleByInvoiceNumberQuery, SaleDetailResponse>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetSaleByInvoiceNumberQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<Result<SaleDetailResponse>> Handle(GetSaleByInvoiceNumberQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.InvoiceNumber))
                return Result<SaleDetailResponse>.Failure(new Error("Sale.InvalidInvoiceNumber", "رقم الفاتورة غير صحيح."));

            var rawNumber = request.InvoiceNumber.Trim();
            var cleanNumber = rawNumber.StartsWith("INV-", StringComparison.OrdinalIgnoreCase)
                ? rawNumber.Substring(4)
                : rawNumber;

            using var connection = _sqlConnectionFactory.CreateConnection();

            var sql = """
                SELECT 
                    s.Id, s.InvoiceNumber, s.SaleDate, s.CashierId,
                    u.FullName AS CashierName, s.CustomerId, c.Name AS CustomerName,
                    s.ShiftId, s.SubTotal, s.DiscountAmount, s.TaxAmount, s.TotalAmount,
                    s.PaidAmount, s.ChangeAmount, s.PaymentMethod,
                    CASE s.Status WHEN 1 THEN 'Completed' WHEN 2 THEN 'Cancelled' ELSE 'Completed' END AS Status,
                    s.Notes
                FROM [Sales].[Sales] s
                LEFT JOIN [Identity].[AspNetUsers] u ON s.CashierId = CAST(u.Id AS uniqueidentifier)
                LEFT JOIN [Sales].[Customers] c ON s.CustomerId = c.Id
                WHERE s.InvoiceNumber = @RawNumber 
                   OR s.InvoiceNumber = @InvNumber
                   OR REPLACE(s.InvoiceNumber, 'INV-', '') = @CleanNumber
                """;

            var saleData = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new
            {
                RawNumber = rawNumber,
                InvNumber = $"INV-{cleanNumber}",
                CleanNumber = cleanNumber
            });

            if (saleData is null)
                return Result<SaleDetailResponse>.Failure(new Error("Sale.NotFound", $"الفاتورة بالرقم '{rawNumber}' غير موجودة."));

            Guid saleId = (Guid)saleData.Id;

            var itemsSql = """
                SELECT 
                    si.Id, si.ProductId, p.NameAr AS ProductName, p.Barcode,
                    si.Quantity,
                    ISNULL((
                        SELECT SUM(sri.Quantity) 
                        FROM [Returns].[SalesReturnItems] sri 
                        JOIN [Returns].[SalesReturns] sr ON sri.SalesReturnId = sr.Id 
                        WHERE sri.OriginalSaleItemId = si.Id AND sr.Status != 2
                    ), 0) AS ReturnedQuantity,
                    si.UnitPrice, si.Discount, si.Tax,
                    (si.Quantity * si.UnitPrice - si.Discount + si.Tax) AS Total,
                    ISNULL(p.BaseUnit, 'قطعة') AS BaseUnit,
                    ISNULL(p.ParentUnit, 'كرتونة') AS ParentUnit,
                    ISNULL(p.ConversionFactor, 1) AS ConversionFactor
                FROM [Sales].[SaleItems] si
                LEFT JOIN [Inventory].[Products] p ON si.ProductId = p.Id
                WHERE si.SaleId = @SaleId
                """;

            var rawItems = await connection.QueryAsync<dynamic>(itemsSql, new { SaleId = saleId });
            var items = new List<SaleDetailItemResponse>();
            foreach (var r in rawItems)
            {
                decimal qty = (decimal)r.Quantity;
                decimal retQty = (decimal)r.ReturnedQuantity;
                decimal remQty = Math.Max(0, qty - retQty);

                items.Add(new SaleDetailItemResponse(
                    (Guid)r.Id,
                    (Guid)r.ProductId,
                    (string?)r.ProductName,
                    (string?)r.Barcode,
                    qty,
                    (decimal)r.UnitPrice,
                    (decimal)r.Discount,
                    (decimal)r.Tax,
                    (decimal)r.Total,
                    retQty,
                    remQty,
                    (string?)r.BaseUnit,
                    (string?)r.ParentUnit,
                    (int)r.ConversionFactor));
            }

            var response = new SaleDetailResponse(
                (Guid)saleData.Id,
                (string)saleData.InvoiceNumber,
                (DateTime)saleData.SaleDate,
                (Guid)saleData.CashierId,
                (string?)saleData.CashierName,
                saleData.CustomerId == null ? (Guid?)null : (Guid)saleData.CustomerId,
                (string?)saleData.CustomerName,
                (Guid)saleData.ShiftId,
                (decimal)saleData.SubTotal,
                (decimal)saleData.DiscountAmount,
                (decimal)saleData.TaxAmount,
                (decimal)saleData.TotalAmount,
                (decimal)saleData.PaidAmount,
                (decimal)saleData.ChangeAmount,
                (string)saleData.PaymentMethod,
                (string)saleData.Status,
                (string?)saleData.Notes,
                items.ToList());

            return Result<SaleDetailResponse>.Success(response);
        }
    }
}
