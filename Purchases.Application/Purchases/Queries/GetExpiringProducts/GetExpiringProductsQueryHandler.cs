using Dapper;
using POS.Shared.Application.Database;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Purchases.Application.Purchases.Queries.GetExpiringProducts
{
    internal sealed class GetExpiringProductsQueryHandler : IQueryHandler<GetExpiringProductsQuery, IReadOnlyList<ExpiringProductResponse>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetExpiringProductsQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<Result<IReadOnlyList<ExpiringProductResponse>>> Handle(GetExpiringProductsQuery request, CancellationToken cancellationToken)
        {
            using var connection = _sqlConnectionFactory.CreateConnection();

            var sql = """
                WITH AllExpiringBatches AS (
                    SELECT 
                        b.Id AS BatchId,
                        b.ProductId,
                        ISNULL(p.NameAr, N'منتج') AS ProductName,
                        ISNULL(p.Barcode, '') AS Barcode,
                        b.ExpiryDate,
                        b.BatchNumber,
                        b.BaseQuantity AS BatchOriginalQuantity,
                        b.RemainingQuantity AS BatchRemainingQuantity,
                        b.RemainingQuantity AS QuantityInStock,
                        ISNULL(p.QuantityInStock, 0) AS TotalProductStock,
                        b.UnitCost,
                        b.PurchaseDate,
                        ISNULL(pur.InvoiceNumber, '') AS InvoiceNumber,
                        ISNULL(s.Name, N'مورد عام') AS SupplierName,
                        DATEDIFF(day, CAST(GETDATE() AS date), CAST(b.ExpiryDate AS date)) AS DaysRemaining,
                        CASE WHEN b.RemainingQuantity <= 0 OR b.Status = 2 THEN 1 ELSE 0 END AS IsDepleted,
                        ISNULL(w.TotalWastedQuantity, 0) AS WastedQuantity
                    FROM [Inventory].[InventoryBatches] b
                    JOIN [Inventory].[Products] p ON b.ProductId = p.Id
                    LEFT JOIN [Purchases].[Purchases] pur ON b.PurchaseInvoiceId = pur.Id
                    LEFT JOIN [Purchases].[Suppliers] s ON pur.SupplierId = s.Id
                    LEFT JOIN (
                        SELECT InventoryBatchId, SUM(BaseQuantity) AS TotalWastedQuantity
                        FROM [Inventory].[InventoryWastes]
                        GROUP BY InventoryBatchId
                    ) w ON b.Id = w.InventoryBatchId
                    WHERE b.ExpiryDate IS NOT NULL

                    UNION ALL

                    SELECT 
                        pi.Id AS BatchId,
                        pi.ProductId,
                        ISNULL(p.NameAr, N'منتج') AS ProductName,
                        ISNULL(p.Barcode, '') AS Barcode,
                        pi.ExpiryDate,
                        pi.BatchNumber,
                        pi.Quantity AS BatchOriginalQuantity,
                        ISNULL(p.QuantityInStock, 0) AS BatchRemainingQuantity,
                        ISNULL(p.QuantityInStock, 0) AS QuantityInStock,
                        ISNULL(p.QuantityInStock, 0) AS TotalProductStock,
                        pi.UnitCost,
                        pur.PurchaseDate,
                        ISNULL(pur.InvoiceNumber, '') AS InvoiceNumber,
                        ISNULL(s.Name, N'مورد عام') AS SupplierName,
                        DATEDIFF(day, CAST(GETDATE() AS date), CAST(pi.ExpiryDate AS date)) AS DaysRemaining,
                        CASE WHEN ISNULL(p.QuantityInStock, 0) <= 0 THEN 1 ELSE 0 END AS IsDepleted,
                        0 AS WastedQuantity
                    FROM [Purchases].[PurchaseItems] pi
                    JOIN [Purchases].[Purchases] pur ON pi.PurchaseId = pur.Id
                    LEFT JOIN [Inventory].[Products] p ON pi.ProductId = p.Id
                    LEFT JOIN [Purchases].[Suppliers] s ON pur.SupplierId = s.Id
                    WHERE pi.ExpiryDate IS NOT NULL
                      AND pi.Id NOT IN (SELECT PurchaseInvoiceItemId FROM [Inventory].[InventoryBatches] WHERE PurchaseInvoiceItemId IS NOT NULL)
                )
                SELECT * FROM AllExpiringBatches
                WHERE 1=1
                """;

            if (request.DaysThreshold.HasValue)
            {
                sql += " AND DaysRemaining <= @DaysThreshold";
            }

            sql += " ORDER BY DaysRemaining ASC, ExpiryDate ASC";

            var items = await connection.QueryAsync<ExpiringProductResponse>(sql, new { request.DaysThreshold });
            return Result<IReadOnlyList<ExpiringProductResponse>>.Success(items.ToList());
        }
    }
}
