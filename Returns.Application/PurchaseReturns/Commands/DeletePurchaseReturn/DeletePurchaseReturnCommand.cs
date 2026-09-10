using POS.Shared.Application.Messaging;

namespace Returns.Application.PurchaseReturns.Commands.DeletePurchaseReturn
{
    public sealed record DeletePurchaseReturnCommand(Guid Id, Guid UserId) : ICommand;
}
