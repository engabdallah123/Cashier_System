using POS.Shared.Application.Messaging;

namespace Sales.Application.Sales.Commands.CreateSale
{
    public sealed record CreateSaleItemRequest(
        Guid ProductId,
        decimal Quantity,
        decimal UnitPrice,
        decimal Discount = 0,
        decimal Tax = 0);

    public sealed record CreateSaleCommand(
        Guid CashierId,
        Guid ShiftId,
        List<CreateSaleItemRequest> Items,
        Guid? CustomerId = null,
        decimal DiscountAmount = 0,
        decimal TaxAmount = 0,
        decimal PaidAmount = 0,
        string PaymentMethod = "Cash",
        string? Notes = null) : ICommand<CreateSaleResult>;
}
