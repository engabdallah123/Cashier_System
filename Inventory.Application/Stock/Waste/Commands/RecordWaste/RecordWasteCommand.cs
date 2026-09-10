using POS.Shared.Application.Messaging;

namespace Inventory.Application.Stock.Waste.Commands.RecordWaste
{
    public sealed record RecordWasteCommand(
        Guid ProductId,
        Guid InventoryBatchId,
        decimal Quantity,
        string Unit,
        string Reason,
        Guid CreatedBy,
        string? Notes = null,
        string Source = "Manual",
        Guid? RelatedNotificationId = null) : ICommand<Guid>;
}
