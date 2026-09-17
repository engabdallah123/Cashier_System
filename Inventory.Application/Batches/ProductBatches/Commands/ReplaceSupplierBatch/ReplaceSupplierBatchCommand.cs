using POS.Shared.Application.Messaging;

namespace Inventory.Application.Batches.ProductBatches.Commands.ReplaceSupplierBatch
{
    public sealed record ReplaceSupplierBatchCommand(
        Guid BatchId,
        DateTime NewExpiryDate,
        string? NewBatchNumber = null,
        string? Notes = null,
        Guid? RelatedNotificationId = null,
        Guid? UserId = null) : ICommand;
}
