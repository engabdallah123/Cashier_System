using POS.Shared.Application.Messaging;

namespace Returns.Application.PurchaseReturns.Queries.GetPurchaseReturnById
{
    public sealed record GetPurchaseReturnByIdQuery(Guid Id) : IQuery<PurchaseReturnResponse>;
}
