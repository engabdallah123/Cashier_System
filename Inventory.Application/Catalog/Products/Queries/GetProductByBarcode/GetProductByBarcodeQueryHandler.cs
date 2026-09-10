using Dapper;
using Inventory.Domain.Catalog.Products.Errors;
using POS.Shared.Application.Database;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Inventory.Application.Catalog.Products.Queries.GetProductByBarcode
{
    internal sealed class GetProductByBarcodeQueryHandler : IQueryHandler<GetProductByBarcodeQuery, ProductResponse>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetProductByBarcodeQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<Result<ProductResponse>> Handle(GetProductByBarcodeQuery request, CancellationToken cancellationToken)
        {
            using var connection = _sqlConnectionFactory.CreateConnection();

            const string sql = """
                SELECT 
                    p.Id, p.Barcode, p.NameAr, p.NameEn, p.Description,
                    p.CategoryId, c.NameAr AS CategoryName,
                    p.UnitId, u.Symbol AS UnitSymbol,
                    p.SupplierId, sup.Name AS SupplierName,
                    p.PurchasePrice, p.SellingPrice, p.WholesalePrice,
                    p.QuantityInStock, p.ReorderLevel, p.MaxStockLevel,
                    p.IsWeighable, p.IsActive, p.TrackExpiry, p.TaxRate, p.ImageUrl,
                    p.CreatedAt, p.UpdatedAt,
                    p.BaseUnit, p.ParentUnit, p.ConversionFactor, p.ShelfLifeDays, p.ExpiryAlertDays
                FROM [Inventory].[Products] p
                LEFT JOIN [Inventory].[Categories] c ON p.CategoryId = c.Id
                LEFT JOIN [Inventory].[Units] u ON p.UnitId = u.Id
                LEFT JOIN [Purchases].[Suppliers] sup ON p.SupplierId = sup.Id
                WHERE p.Barcode = @Barcode
                """;

            var product = await connection.QuerySingleOrDefaultAsync<ProductResponse>(sql, new { request.Barcode });

            if (product is null)
                return Result<ProductResponse>.Failure(ProductErrors.NotFoundByBarcode(request.Barcode));

            return Result<ProductResponse>.Success(product);
        }
    }
}
