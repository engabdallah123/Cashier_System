namespace Inventory.Application.Stock.Waste.Queries.GetWasteReport
{
    public sealed record WasteItemDto(
        Guid Id,
        Guid ProductId,
        string ProductName,
        string ProductBarcode,
        Guid InventoryBatchId,
        string BatchNumber,
        decimal Quantity,
        string Unit,
        decimal BaseQuantity,
        decimal UnitCost,
        decimal TotalCost,
        string Reason,
        string Source,
        string? Notes,
        DateTime CreatedAt);

    public sealed record WasteReportResponse(
        IReadOnlyList<WasteItemDto> Items,
        decimal TotalLossAmount,
        int TotalRecordsCount);
}
