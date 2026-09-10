using POS.Shared.Application.Messaging;

namespace Purchases.Application.Purchases.Commands.DeletePurchase
{
    public sealed record DeletePurchaseCommand(Guid Id) : ICommand;
}
