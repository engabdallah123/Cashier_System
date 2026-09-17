namespace POS.Desktop.Services.Sync
{
    public class SyncStatusResult
    {
        public bool Success { get; set; }
        public int SyncedPurchasesCount { get; set; }
        public int SyncedCatalogCount { get; set; }
        public int SyncedDebtsCount { get; set; }
        public string? Message { get; set; }
        public string? Error { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public record CloudPurchaseSyncItemDto(
        Guid Id,
        Guid ProductId,
        string ProductName,
        string? Barcode,
        decimal Quantity,
        decimal UnitCost,
        decimal Discount,
        decimal Tax,
        decimal Total,
        DateTime? ExpiryDate,
        string? BatchNumber,
        string? Unit);

    public record CloudPurchaseSyncDto(
        Guid Id,
        string InvoiceNumber,
        string? InternalNumber,
        Guid SupplierId,
        string? SupplierName,
        DateTime PurchaseDate,
        decimal SubTotal,
        decimal DiscountAmount,
        decimal TaxAmount,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal RemainingAmount,
        int PaymentMethod,
        string? Notes,
        string? CreatedByName,
        string SyncStatus,
        DateTime? SyncedAt,
        string? SyncError,
        int SyncAttempts,
        DateTime CreatedAt,
        List<CloudPurchaseSyncItemDto> Items);

    public record AcknowledgeCloudSyncRequest(
        Guid PurchaseId,
        string? LocalReference = null);

    public record FailCloudSyncRequest(
        Guid PurchaseId,
        string ErrorMessage);

    public record PushCloudCatalogRequest(
        List<CatalogSupplierSyncItem>? Suppliers,
        List<CatalogProductSyncItem>? Products,
        List<CatalogCategorySyncItem>? Categories = null);

    public record CatalogCategorySyncItem(
        Guid Id,
        string NameAr,
        string? NameEn = null);

    public record CatalogSupplierSyncItem(
        Guid Id,
        string Name,
        string? Phone,
        string? Address,
        decimal Balance,
        string? Email = null,
        string? ContactPerson = null);

    public record PushStoreSettingsRequest(
        string StoreName,
        string? Address,
        string? Phone,
        decimal TaxRate,
        bool IsTaxIncluded,
        string Currency,
        string? InvoiceFooterMessage,
        bool AllowNegativeStock,
        string? LogoUrl);

    public record CatalogProductSyncItem(
        Guid Id,
        string Barcode,
        string NameAr,
        string? NameEn,
        string BaseUnit,
        string? ParentUnit,
        int ConversionFactor,
        decimal PurchasePrice,
        decimal SellingPrice,
        decimal WholesalePrice,
        decimal StockQuantity,
        Guid? CategoryId,
        string? CategoryName,
        bool IsWeighable,
        int ShelfLifeDays,
        int ExpiryAlertDays,
        decimal ReorderLevel,
        bool TrackExpiry);

    public record StockUpdateItem(
        Guid ProductId,
        decimal NewStockQuantity);

    public record PushStockRequest(
        List<StockUpdateItem> Items);

    public record SupplierBalanceUpdateItem(
        Guid SupplierId,
        decimal NewBalance);

    public record PushSupplierBalancesRequest(
        List<SupplierBalanceUpdateItem> Items);

    public record DebtItemSyncDto(
        Guid Id,
        string Type,
        Guid ReferenceId,
        string InvoiceNumber,
        string EntityName,
        string? Phone,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal RemainingAmount,
        DateTime Date);

    public record PushDebtsRequest(
        List<DebtItemSyncDto> Debts);

    public record PushDashboardSnapshotRequest(
        decimal TodaySales,
        decimal TodayProfit,
        decimal TodayPurchases,
        decimal TodayExpenses,
        decimal MonthSales,
        decimal MonthProfit,
        decimal MonthPurchases,
        decimal MonthExpenses,
        decimal CustomerDebtsTotal,
        decimal SupplierDebtsTotal,
        int LowStockCount,
        int ExpiryAlertsCount,
        string? MonthlySalesJson,
        decimal TodayWasteLoss = 0,
        decimal MonthWasteLoss = 0,
        decimal TotalWasteLoss = 0);

    public record PendingDebtPaymentDto(
        Guid Id,
        string DebtType,
        Guid ReferenceId,
        decimal Amount,
        string? Notes,
        DateTime CreatedAt);

    public record PendingCloudProductDto(
        Guid Id,
        string Barcode,
        string NameAr,
        string? NameEn,
        string BaseUnit,
        string? ParentUnit,
        int ConversionFactor,
        decimal PurchasePrice,
        decimal SellingPrice,
        decimal WholesalePrice,
        decimal StockQuantity,
        Guid? CategoryId,
        string? CategoryName,
        bool IsWeighable,
        int ShelfLifeDays,
        int ExpiryAlertDays,
        decimal ReorderLevel,
        bool TrackExpiry);

    public record PendingCloudSupplierDto(
        Guid Id,
        string Name,
        string Phone,
        string? Email,
        string? Address,
        string? ContactPerson);

    // Expenses Sync DTOs
    public record CloudExpenseSyncDto(
        Guid Id,
        decimal Amount,
        string Category,
        string? Description,
        DateTime Date,
        string? Notes);

    public record PushExpensesRequest(
        List<CloudExpenseSyncDto> Expenses);

    // Expiry Notifications Sync DTOs
    public record CloudExpiryNotificationItemDto(
        Guid Id,
        Guid ProductId,
        string ProductName,
        string? Barcode,
        Guid? BatchId,
        string? BatchNumber,
        decimal RemainingQuantity,
        string Unit,
        decimal UnitCost,
        DateTime? ExpiryDate,
        int DaysRemaining,
        bool IsExpired,
        string Message,
        string Status);

    public record PushExpiryNotificationsRequest(
        List<CloudExpiryNotificationItemDto> Notifications);

    public record PendingNotificationActionDto(
        Guid NotificationId,
        Guid ProductId,
        Guid? BatchId,
        string ActionType,
        decimal Quantity,
        string? Reason,
        DateTime? NewExpiryDate,
        string? NewBatchNumber,
        string? Notes,
        DateTime ActionTakenAt);

    // Shift Summary DTOs
    public record PushClosedShiftRequest(
        Guid ShiftId,
        string CashierName,
        DateTime OpenedAt,
        DateTime ClosedAt,
        decimal OpeningCash,
        decimal ActualClosingCash,
        decimal SystemCash,
        decimal CashDifference,
        decimal TotalSales,
        decimal TotalCash,
        decimal TotalCard,
        decimal TotalWallet,
        decimal TotalCredit,
        int TotalInvoices,
        int TotalReturns,
        string? ClosingNotes);

    public record PendingCloudCategoryDto(
        Guid Id,
        string NameAr,
        string? NameEn,
        bool IsActive,
        DateTime CreatedAt);

    public record CloudSyncStatusDto(
        bool HasPending,
        bool HasPendingPurchases,
        bool HasPendingSuppliers,
        bool HasPendingDebts,
        bool HasPendingNotificationActions,
        bool HasPendingCategories,
        DateTime ServerTime);

    public record ReturnItemSyncDto(
        Guid Id,
        Guid ProductId,
        string ProductName,
        string? Barcode,
        decimal Quantity,
        decimal UnitPrice,
        decimal Tax,
        decimal Total,
        string? Reason);

    public record PushReturnSyncItem(
        Guid Id,
        string ReturnNumber,
        string Type,
        string? OriginalInvoiceNumber,
        string? PartyName,
        string? PartyPhone,
        DateTime ReturnDate,
        decimal TotalAmount,
        string RefundMethod,
        string? Reason,
        string? Notes,
        string? ItemsJson,
        int ItemsCount);

    public record PushReturnsRequest(List<PushReturnSyncItem> Returns);
}
