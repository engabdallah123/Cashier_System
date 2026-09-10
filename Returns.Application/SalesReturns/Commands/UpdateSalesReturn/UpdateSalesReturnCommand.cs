using POS.Shared.Application.Messaging;

namespace Returns.Application.SalesReturns.Commands.UpdateSalesReturn
{
    public sealed record UpdateSalesReturnItemRequest(
        Guid ProductId,
        Guid OriginalSaleItemId,
        decimal Quantity,
        decimal UnitPrice,
        decimal Tax = 0,
        string? Reason = null);

    public sealed record UpdateSalesReturnCommand(
        Guid Id,
        int RefundMethod,
        string? Reason,
        string? Notes,
        List<UpdateSalesReturnItemRequest> Items,
        Guid UserId) : ICommand;
}
