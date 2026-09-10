using Dapper;
using POS.Shared.Application.Database;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Inventory.Application.Catalog.Products.Queries.GetProducts
{
    internal sealed class GetProductsQueryHandler : IQueryHandler<GetProductsQuery, IReadOnlyList<ProductResponse>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetProductsQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<Result<IReadOnlyList<ProductResponse>>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
        {
            using var connection = _sqlConnectionFactory.CreateConnection();

            var sql = """
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
                WHERE 1 = 1
                """;

            if (request.CategoryId.HasValue)
                sql += " AND p.CategoryId = @CategoryId";

            if (request.IsActive.HasValue)
                sql += " AND p.IsActive = @IsActive";

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                sql += " AND (p.NameAr LIKE @SearchStr OR p.NameEn LIKE @SearchStr OR p.Barcode LIKE @SearchStr)";

            sql += " ORDER BY p.CreatedAt DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var offset = (request.Page - 1) * request.PageSize;

            var products = await connection.QueryAsync<ProductResponse>(sql, new
            {
                request.CategoryId,
                request.IsActive,
                SearchStr = $"%{request.SearchTerm}%",
                Offset = offset,
                request.PageSize
            });

            var resultList = (IReadOnlyList<ProductResponse>)products.ToList();
            return Result<IReadOnlyList<ProductResponse>>.Success(resultList);
        }
    }
}
