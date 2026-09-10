using Dapper;
using POS.Shared.Application.Database;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Returns.Domain.Returns;

namespace Returns.Application.PurchaseReturns.Queries.GetPurchaseReturnById
{
    internal sealed class GetPurchaseReturnByIdQueryHandler : IQueryHandler<GetPurchaseReturnByIdQuery, PurchaseReturnResponse>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetPurchaseReturnByIdQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<Result<PurchaseReturnResponse>> Handle(GetPurchaseReturnByIdQuery request, CancellationToken cancellationToken)
        {
            using var connection = _sqlConnectionFactory.CreateConnection();

            const string returnSql = """
                SELECT 
                    pr.Id, pr.ReturnNumber, pr.OriginalPurchaseId,
                    p.InvoiceNumber AS OriginalInvoiceNumber,
                    pr.SupplierId, s.Name AS SupplierName,
                    pr.ReturnDate, pr.SubTotal, pr.TaxAmount, pr.TotalAmount,
                    pr.Reason, pr.Notes,
                    CASE pr.Status WHEN 1 THEN 'Completed' WHEN 2 THEN 'Cancelled' ELSE 'Completed' END AS Status,
                    pr.CreatedByUserId,
                    u.FullName AS CreatedByUserName
                FROM [Returns].[PurchaseReturns] pr
                LEFT JOIN [Purchases].[Purchases] p ON pr.OriginalPurchaseId = p.Id
                LEFT JOIN [Purchases].[Suppliers] s ON pr.SupplierId = s.Id
                LEFT JOIN [Identity].[AspNetUsers] u ON pr.CreatedByUserId = TRY_CAST(u.Id AS uniqueidentifier)
                WHERE pr.Id = @Id
                """;

            var data = await connection.QueryFirstOrDefaultAsync<dynamic>(returnSql, new { request.Id });

            if (data is null)
                return Result<PurchaseReturnResponse>.Failure(ReturnErrors.NotFound(request.Id));

            const string itemsSql = """
                SELECT 
                    pri.Id,
                    pri.ProductId,
                    prod.NameAr AS ProductName,
                    prod.Barcode,
                    pri.Quantity,
                    pri.UnitCost,
                    pri.Tax,
                    (pri.Quantity * pri.UnitCost + pri.Tax) AS Total
                FROM [Returns].[PurchaseReturnItems] pri
                LEFT JOIN [Inventory].[Products] prod ON pri.ProductId = prod.Id
                WHERE pri.PurchaseReturnId = @Id
                """;

            var items = (await connection.QueryAsync<PurchaseReturnItemResponse>(itemsSql, new { request.Id })).ToList();

            var fullResponse = new PurchaseReturnResponse(
                (Guid)data.Id,
                (string)data.ReturnNumber,
                (Guid)data.OriginalPurchaseId,
                (string?)data.OriginalInvoiceNumber,
                (Guid)data.SupplierId,
                (string?)data.SupplierName,
                (DateTime)data.ReturnDate,
                (decimal)data.SubTotal,
                (decimal)data.TaxAmount,
                (decimal)data.TotalAmount,
                (string?)data.Reason,
                (string?)data.Notes,
                (string)data.Status,
                (Guid)data.CreatedByUserId,
                (string?)data.CreatedByUserName,
                items);

            return Result<PurchaseReturnResponse>.Success(fullResponse);
        }
    }
}
