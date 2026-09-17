using Dapper;
using POS.Shared.Application.Database;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Purchases.Application.Purchases.Queries.GetPurchases
{
    internal sealed class GetPurchasesQueryHandler : IQueryHandler<GetPurchasesQuery, IReadOnlyList<PurchaseResponse>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetPurchasesQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<Result<IReadOnlyList<PurchaseResponse>>> Handle(GetPurchasesQuery request, CancellationToken cancellationToken)
        {
            using var connection = _sqlConnectionFactory.CreateConnection();

            var sql = """
                SELECT 
                    p.Id, p.InvoiceNumber, p.InternalNumber, p.SupplierId,
                    s.Name AS SupplierName, p.PurchaseDate, p.ReceivedDate,
                    p.SubTotal, p.DiscountAmount, p.TaxAmount, p.TotalAmount,
                    p.PaidAmount, p.RemainingAmount,
                    CASE p.PaymentMethod 
                        WHEN 1 THEN N'نقداً' WHEN 2 THEN N'بطاقة بنكية' WHEN 3 THEN N'محفظة إلكترونية' WHEN 4 THEN N'آجل' ELSE N'نقداً' 
                    END AS PaymentMethod,
                    CASE p.Status 
                        WHEN 1 THEN 'Draft' WHEN 2 THEN 'Received' WHEN 3 THEN 'Cancelled' ELSE 'Draft' 
                    END AS Status,
                    p.Notes, p.CreatedByUserId
                FROM [Purchases].[Purchases] p
                LEFT JOIN [Purchases].[Suppliers] s ON p.SupplierId = s.Id
                WHERE 1 = 1
                """;

            if (request.SupplierId.HasValue)
                sql += " AND p.SupplierId = @SupplierId";

            sql += " ORDER BY p.PurchaseDate DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var offset = (request.Page - 1) * request.PageSize;

            var purchases = await connection.QueryAsync<PurchaseResponse>(sql, new
            {
                request.SupplierId,
                Offset = offset,
                request.PageSize
            });

            return Result<IReadOnlyList<PurchaseResponse>>.Success(purchases.ToList());
        }
    }
}
