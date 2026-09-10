using POS.Shared.Application.Messaging;

namespace Returns.Application.SalesReturns.Commands.DeleteSalesReturn
{
    public sealed record DeleteSalesReturnCommand(Guid Id, Guid UserId) : ICommand;
}
