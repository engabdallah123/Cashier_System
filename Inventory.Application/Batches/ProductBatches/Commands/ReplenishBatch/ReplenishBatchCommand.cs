using POS.Shared.Application.Messaging;

namespace Inventory.Application.Batches.ProductBatches.Commands.ReplenishBatch
{
    public sealed record ReplenishBatchCommand(
        Guid BatchId,
        decimal Quantity,
        string? Notes = null,
        Guid? UserId = null) : ICommand;
}
