namespace Inventory.Application.Batches.Queries.GetProductBatches
{
    public sealed record ProductBatchDto(
        Guid Id,
        Guid ProductId,
        string BatchNumber,
        decimal OriginalQuantity,
        string OriginalUnit,
        decimal BaseQuantity,
        decimal RemainingQuantity,
        decimal RemainingInParentUnit,
        decimal UnitCost,
        decimal CartonCost,
        DateTime PurchaseDate,
        DateTime? ExpiryDate,
        int? DaysUntilExpiry,
        string Status);
}
