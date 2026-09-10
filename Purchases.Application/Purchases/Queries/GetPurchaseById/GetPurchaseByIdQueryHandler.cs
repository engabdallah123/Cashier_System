using Dapper;
using POS.Shared.Application.Database;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Purchases.Application.Purchases.Queries.GetPurchaseById
{
    internal sealed class GetPurchaseByIdQueryHandler : IQueryHandler<GetPurchaseByIdQuery, PurchaseDetailResponse>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetPurchaseByIdQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<Result<PurchaseDetailResponse>> Handle(GetPurchaseByIdQuery request, CancellationToken cancellationToken)
        {
            using var connection = _sqlConnectionFactory.CreateConnection();

            var sql = """
                SELECT 
                    p.Id, p.InvoiceNumber, p.InternalNumber, p.PurchaseDate,
                    p.SupplierId, s.Name AS SupplierName,
                    p.SubTotal, p.DiscountAmount, p.TaxAmount, p.TotalAmount,
                    p.PaidAmount, p.RemainingAmount,
                    CASE p.Status WHEN 1 THEN 'Draft' WHEN 2 THEN 'Received' WHEN 3 THEN 'Cancelled' ELSE 'Draft' END AS Status,
                    p.Notes
                FROM [Purchases].[Purchases] p
                LEFT JOIN [Purchases].[Suppliers] s ON p.SupplierId = s.Id
                WHERE p.Id = @Id
                """;

            var itemsSql = """
                SELECT 
                    pi2.Id, pi2.ProductId, pr.NameAr AS ProductName, pr.Barcode,
                    pi2.Quantity,
                    ISNULL((
                        SELECT SUM(pri.Quantity) 
                        FROM [Returns].[PurchaseReturnItems] pri 
                        JOIN [Returns].[PurchaseReturns] prt ON pri.PurchaseReturnId = prt.Id 
                        WHERE prt.OriginalPurchaseId = pi2.PurchaseId AND pri.ProductId = pi2.ProductId AND prt.Status != 2
                    ), 0) AS ReturnedQuantity,
                    pi2.UnitCost, pi2.Discount, pi2.Tax,
                    (pi2.Quantity * pi2.UnitCost - pi2.Discount + pi2.Tax) AS Total,
                    pi2.ExpiryDate, pi2.BatchNumber,
                    ISNULL(pr.BaseUnit, 'قطعة') AS BaseUnit,
                    ISNULL(pr.ParentUnit, 'كرتونة') AS ParentUnit,
                    ISNULL(pr.ConversionFactor, 1) AS ConversionFactor
                FROM [Purchases].[PurchaseItems] pi2
                LEFT JOIN [Inventory].[Products] pr ON pi2.ProductId = pr.Id
                WHERE pi2.PurchaseId = @PurchaseId
                """;

            var purchaseData = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { request.Id });

            if (purchaseData is null)
                return Result<PurchaseDetailResponse>.Failure(new Error("Purchase.NotFound", $"فاتورة الشراء بالرقم '{request.Id}' غير موجودة."));

            var rawItems = await connection.QueryAsync<dynamic>(itemsSql, new { PurchaseId = request.Id });
            var items = new List<PurchaseDetailItemResponse>();
            foreach (var r in rawItems)
            {
                decimal qty = (decimal)r.Quantity;
                decimal retQty = (decimal)r.ReturnedQuantity;
                decimal remQty = Math.Max(0, qty - retQty);

                items.Add(new PurchaseDetailItemResponse(
                    (Guid)r.Id,
                    (Guid)r.ProductId,
                    (string?)r.ProductName,
                    (string?)r.Barcode,
                    qty,
                    (decimal)r.UnitCost,
                    (decimal)r.Discount,
                    (decimal)r.Tax,
                    (decimal)r.Total,
                    (DateTime?)r.ExpiryDate,
                    (string?)r.BatchNumber,
                    retQty,
                    remQty,
                    (string?)r.BaseUnit,
                    (string?)r.ParentUnit,
                    (int)r.ConversionFactor));
            }

            var response = new PurchaseDetailResponse(
                (Guid)purchaseData.Id,
                (string)purchaseData.InvoiceNumber,
                (string?)purchaseData.InternalNumber,
                (DateTime)purchaseData.PurchaseDate,
                (Guid)purchaseData.SupplierId,
                (string?)purchaseData.SupplierName,
                (decimal)purchaseData.SubTotal,
                (decimal)purchaseData.DiscountAmount,
                (decimal)purchaseData.TaxAmount,
                (decimal)purchaseData.TotalAmount,
                (decimal)purchaseData.PaidAmount,
                (decimal)purchaseData.RemainingAmount,
                (string)purchaseData.Status,
                (string?)purchaseData.Notes,
                items.ToList());

            return Result<PurchaseDetailResponse>.Success(response);
        }
    }
}
