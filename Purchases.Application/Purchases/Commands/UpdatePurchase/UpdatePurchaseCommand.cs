using POS.Shared.Application.Messaging;
using Purchases.Application.Purchases.Commands.CreatePurchase;

namespace Purchases.Application.Purchases.Commands.UpdatePurchase
{
    public sealed record UpdatePurchaseCommand(
        Guid Id,
        string InvoiceNumber,
        Guid SupplierId,
        Guid UserId,
        List<CreatePurchaseItemRequest> Items,
        string? InternalNumber = null,
        decimal DiscountAmount = 0,
        decimal TaxAmount = 0,
        decimal PaidAmount = 0,
        int PaymentMethod = 1,
        string? Notes = null,
        DateTime? PurchaseDate = null) : ICommand;
}
