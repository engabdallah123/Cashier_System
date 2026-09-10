using POS.Shared.Application.Messaging;

namespace Returns.Application.SalesReturns.Queries.GetSalesReturnById
{
    public sealed record GetSalesReturnByIdQuery(Guid Id) : IQuery<SalesReturnResponse>;
}
