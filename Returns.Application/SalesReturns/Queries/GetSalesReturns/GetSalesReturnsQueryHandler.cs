using Dapper;
using POS.Shared.Application.Database;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Returns.Application.SalesReturns.Queries.GetSalesReturns
{
    internal sealed class GetSalesReturnsQueryHandler : IQueryHandler<GetSalesReturnsQuery, IReadOnlyList<SalesReturnResponse>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetSalesReturnsQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<Result<IReadOnlyList<SalesReturnResponse>>> Handle(GetSalesReturnsQuery request, CancellationToken cancellationToken)
        {
            using var connection = _sqlConnectionFactory.CreateConnection();

            var sql = """
                SELECT 
                    sr.Id, sr.ReturnNumber, sr.OriginalSaleId, sr.CashierId,
                    u.FullName AS CashierName, sr.CustomerId, c.Name AS CustomerName,
                    sr.ShiftId, sr.ReturnDate, sr.SubTotal, sr.TaxAmount, sr.TotalAmount,
                    CASE sr.RefundMethod WHEN 1 THEN 'Cash' WHEN 2 THEN 'Card' WHEN 3 THEN 'StoreCredit' WHEN 4 THEN 'Exchange' ELSE 'Cash' END AS RefundMethod,
                    sr.Reason, sr.Notes,
                    CASE sr.Status WHEN 1 THEN 'Completed' WHEN 2 THEN 'Cancelled' ELSE 'Completed' END AS Status
                FROM [Returns].[SalesReturns] sr
                LEFT JOIN [Identity].[AspNetUsers] u ON sr.CashierId = TRY_CAST(u.Id AS uniqueidentifier)
                LEFT JOIN [Sales].[Customers] c ON sr.CustomerId = c.Id
                WHERE 1 = 1
                """;

            if (request.CashierId.HasValue)
                sql += " AND sr.CashierId = @CashierId";

            if (request.ShiftId.HasValue)
                sql += " AND sr.ShiftId = @ShiftId";

            sql += " ORDER BY sr.ReturnDate DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var offset = (request.Page - 1) * request.PageSize;

            var returns = await connection.QueryAsync<SalesReturnResponse>(sql, new
            {
                request.CashierId,
                request.ShiftId,
                Offset = offset,
                request.PageSize
            });

            return Result<IReadOnlyList<SalesReturnResponse>>.Success(returns.ToList());
        }
    }
}
