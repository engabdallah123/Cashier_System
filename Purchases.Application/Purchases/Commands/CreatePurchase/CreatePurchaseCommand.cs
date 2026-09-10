using POS.Shared.Application.Messaging;

namespace Purchases.Application.Purchases.Commands.CreatePurchase
{
    public sealed record CreatePurchaseItemRequest(
        Guid ProductId,
        decimal Quantity,
        decimal UnitCost,
        decimal Discount = 0,
        decimal Tax = 0,
        DateTime? ExpiryDate = null,
        string? BatchNumber = null,
        string? Unit = null);

    public sealed record CreatePurchaseCommand(
        string InvoiceNumber,
        Guid SupplierId,
        Guid CreatedByUserId,
        List<CreatePurchaseItemRequest> Items,
        string? InternalNumber = null,
        decimal DiscountAmount = 0,
        decimal TaxAmount = 0,
        decimal PaidAmount = 0,
        int PaymentMethod = 1,
        string? Notes = null,
        DateTime? PurchaseDate = null) : ICommand<Guid>;
}
