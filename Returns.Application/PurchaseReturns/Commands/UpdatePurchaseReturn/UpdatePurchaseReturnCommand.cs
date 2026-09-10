using POS.Shared.Application.Messaging;

namespace Returns.Application.PurchaseReturns.Commands.UpdatePurchaseReturn
{
    public sealed record UpdatePurchaseReturnItemRequest(
        Guid ProductId,
        decimal Quantity,
        decimal UnitCost,
        decimal Tax = 0);

    public sealed record UpdatePurchaseReturnCommand(
        Guid Id,
        string? Reason,
        string? Notes,
        List<UpdatePurchaseReturnItemRequest> Items,
        Guid UserId) : ICommand;
}
