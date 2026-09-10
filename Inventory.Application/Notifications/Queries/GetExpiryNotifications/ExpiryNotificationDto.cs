namespace Inventory.Application.Notifications.Queries.GetExpiryNotifications
{
    public sealed record ExpiryNotificationDto(
        Guid Id,
        Guid ProductId,
        string ProductName,
        string ProductBarcode,
        Guid BatchId,
        string BatchNumber,
        decimal RemainingQuantity,
        string BaseUnit,
        string? ParentUnit,
        int ConversionFactor,
        decimal UnitCost,
        DateTime? ExpiryDate,
        int DaysRemaining,
        bool IsExpired,
        string Message,
        string Status,
        DateTime CreatedAt);
}
