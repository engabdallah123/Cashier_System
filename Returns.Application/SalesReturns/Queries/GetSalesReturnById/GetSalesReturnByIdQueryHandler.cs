using Dapper;
using POS.Shared.Application.Database;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Returns.Domain.Returns;

namespace Returns.Application.SalesReturns.Queries.GetSalesReturnById
{
    internal sealed class GetSalesReturnByIdQueryHandler : IQueryHandler<GetSalesReturnByIdQuery, SalesReturnResponse>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetSalesReturnByIdQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<Result<SalesReturnResponse>> Handle(GetSalesReturnByIdQuery request, CancellationToken cancellationToken)
        {
            using var connection = _sqlConnectionFactory.CreateConnection();

            const string returnSql = """
                SELECT 
                    sr.Id, sr.ReturnNumber, sr.OriginalSaleId,
                    s.InvoiceNumber AS OriginalInvoiceNumber,
                    sr.CashierId, u.FullName AS CashierName,
                    sr.CustomerId, c.Name AS CustomerName,
                    sr.ShiftId, sr.ReturnDate, sr.SubTotal, sr.TaxAmount, sr.TotalAmount,
                    CASE sr.RefundMethod WHEN 1 THEN 'Cash' WHEN 2 THEN 'Card' WHEN 3 THEN 'StoreCredit' WHEN 4 THEN 'Exchange' ELSE 'Cash' END AS RefundMethod,
                    sr.Reason, sr.Notes,
                    CASE sr.Status WHEN 1 THEN 'Completed' WHEN 2 THEN 'Cancelled' ELSE 'Completed' END AS Status
                FROM [Returns].[SalesReturns] sr
                LEFT JOIN [Sales].[Sales] s ON sr.OriginalSaleId = s.Id
                LEFT JOIN [Identity].[AspNetUsers] u ON sr.CashierId = TRY_CAST(u.Id AS uniqueidentifier)
                LEFT JOIN [Sales].[Customers] c ON sr.CustomerId = c.Id
                WHERE sr.Id = @Id
                """;

            var data = await connection.QueryFirstOrDefaultAsync<dynamic>(returnSql, new { request.Id });

            if (data is null)
                return Result<SalesReturnResponse>.Failure(ReturnErrors.NotFound(request.Id));

            const string itemsSql = """
                SELECT 
                    sri.Id,
                    sri.ProductId,
                    p.NameAr AS ProductName,
                    p.Barcode,
                    sri.OriginalSaleItemId,
                    sri.Quantity,
                    sri.UnitPrice,
                    sri.Tax,
                    (sri.Quantity * sri.UnitPrice + sri.Tax) AS Total,
                    sri.Reason
                FROM [Returns].[SalesReturnItems] sri
                LEFT JOIN [Inventory].[Products] p ON sri.ProductId = p.Id
                WHERE sri.SalesReturnId = @Id
                """;

            var items = (await connection.QueryAsync<SalesReturnItemResponse>(itemsSql, new { request.Id })).ToList();

            var fullResponse = new SalesReturnResponse(
                (Guid)data.Id,
                (string)data.ReturnNumber,
                (Guid)data.OriginalSaleId,
                (string?)data.OriginalInvoiceNumber,
                (Guid)data.CashierId,
                (string?)data.CashierName,
                (Guid?)data.CustomerId,
                (string?)data.CustomerName,
                (Guid)data.ShiftId,
                (DateTime)data.ReturnDate,
                (decimal)data.SubTotal,
                (decimal)data.TaxAmount,
                (decimal)data.TotalAmount,
                (string)data.RefundMethod,
                (string?)data.Reason,
                (string?)data.Notes,
                (string)data.Status,
                items);

            return Result<SalesReturnResponse>.Success(fullResponse);
        }
    }
}
