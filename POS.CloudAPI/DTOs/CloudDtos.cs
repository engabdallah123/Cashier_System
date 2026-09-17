using POS.CloudAPI.Entities;

namespace POS.CloudAPI.DTOs
{
    // Auth
    public record LoginRequest(string Username, string Password, string? ShopCode = null);

    public record AuthResponseDto(
        string Token,
        Guid UserId,
        string FullName,
        string Role,
        Guid TenantId,
        string TenantName,
        string TenantCode,
        DateTime ExpiresAt);

    // Dashboard
    public record DashboardStatsDto(
        decimal TodaySalesAmount,
        decimal TodayProfitAmount,
        decimal TodayPurchasesAmount,
        int TodayPurchasesCount,
        decimal TodayExpensesAmount,
        decimal MonthSalesAmount,
        decimal MonthProfitAmount,
        decimal MonthPurchasesAmount,
        int MonthPurchasesCount,
        decimal MonthExpensesAmount,
        decimal CustomerDebtsTotal,
        decimal SupplierDebtsTotal,
        int LowStockProductsCount,
        int ExpiryAlertsCount,
        int PendingSyncPurchasesCount,
        int SyncedPurchasesCount,
        int FailedSyncPurchasesCount,
        int TotalSuppliersCount,
        int TotalProductsCount,
        string? MonthlySalesJson,
        List<CloudPurchaseSummaryDto> RecentPurchases,
        decimal TodayWasteLossAmount = 0,
        decimal MonthWasteLossAmount = 0,
        decimal TotalWasteLossAmount = 0);

    // Purchases
    public record CreateCloudPurchaseItemRequest(
        Guid ProductId,
        string ProductName,
        string? Barcode,
        decimal Quantity,
        decimal UnitCost,
        decimal Discount = 0,
        decimal Tax = 0,
        DateTime? ExpiryDate = null,
        string? BatchNumber = null,
        string? Unit = null);

    public record CreateCloudPurchaseRequest(
        Guid? Id, // Optional client-generated Guid for Idempotency
        string InvoiceNumber,
        string? InternalNumber,
        Guid SupplierId,
        DateTime? PurchaseDate,
        decimal DiscountAmount,
        decimal TaxAmount,
        decimal PaidAmount,
        int PaymentMethod,
        string? Notes,
        List<CreateCloudPurchaseItemRequest> Items);

    public record CloudPurchaseSummaryDto(
        Guid Id,
        string InvoiceNumber,
        string? InternalNumber,
        Guid SupplierId,
        string? SupplierName,
        DateTime PurchaseDate,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal RemainingAmount,
        int PaymentMethod,
        string? Notes,
        string SyncStatus,
        DateTime? SyncedAt,
        string? SyncError,
        DateTime CreatedAt,
        int ItemsCount);

    public record CloudPurchaseDetailDto(
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
        List<CloudPurchaseItemDto> Items);

    public record CloudPurchaseItemDto(
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

    // Suppliers
    public record CloudSupplierDto(
        Guid Id,
        string Name,
        string? Phone,
        string? Email,
        string? Address,
        string? ContactPerson,
        decimal Balance,
        bool IsActive,
        string SyncStatus);

    public record CreateCloudSupplierRequest(
        string Name,
        string Phone,
        string? Email = null,
        string? Address = null,
        string? ContactPerson = null,
        decimal Balance = 0);

    public record PendingCloudSupplierDto(
        Guid Id,
        string Name,
        string Phone,
        string? Email,
        string? Address,
        string? ContactPerson,
        decimal Balance);

    // Categories
    public record CloudCategoryDto(
        Guid Id,
        string NameAr,
        string? NameEn,
        bool IsActive,
        string SyncStatus);

    public record CreateCloudCategoryRequest(
        string NameAr,
        string? NameEn);

    // Products
    public record CloudProductDto(
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
        bool TrackExpiry,
        bool IsActive,
        string SyncStatus);

    public record CreateCloudProductRequest(
        string Barcode,
        string NameAr,
        string? NameEn,
        string BaseUnit,
        string? ParentUnit,
        int ConversionFactor,
        decimal PurchasePrice,
        decimal SellingPrice,
        decimal WholesalePrice = 0,
        decimal InitialStock = 0,
        Guid? CategoryId = null,
        string? CategoryName = null,
        bool IsWeighable = false,
        int ShelfLifeDays = 30,
        int ExpiryAlertDays = 3,
        decimal ReorderLevel = 5,
        bool TrackExpiry = false);

    public record UpdateCloudProductRequest(
        string Barcode,
        string NameAr,
        string? NameEn,
        string BaseUnit,
        string? ParentUnit,
        int ConversionFactor,
        decimal PurchasePrice,
        decimal SellingPrice,
        decimal WholesalePrice,
        Guid? CategoryId,
        string? CategoryName,
        bool IsWeighable,
        int ShelfLifeDays,
        int ExpiryAlertDays,
        decimal ReorderLevel,
        bool TrackExpiry);

    // Debts
    public record CloudDebtDto(
        Guid Id,
        string Type, // "Customer" or "Supplier"
        Guid ReferenceId,
        string InvoiceNumber,
        string EntityName,
        string? Phone,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal RemainingAmount,
        DateTime Date);

    public record PayDebtRequest(
        string DebtType, // "Customer" or "Supplier"
        Guid ReferenceId,
        decimal Amount,
        string? Notes = null);

    public record DebtPaymentSyncDto(
        Guid Id,
        string DebtType,
        Guid ReferenceId,
        decimal Amount,
        string? Notes,
        DateTime CreatedAt);

    // Expenses
    public record CloudExpenseDto(
        Guid Id,
        string Title,
        decimal Amount,
        string? Category,
        DateTime Date,
        string? Notes,
        string SyncStatus);

    public record CreateCloudExpenseRequest(
        string Title,
        decimal Amount,
        string? Category,
        DateTime? Date,
        string? Notes);

    // Sync DTOs for Desktop POS
    public record AcknowledgeSyncRequest(
        Guid PurchaseId,
        string? LocalReference = null);

    public record FailSyncRequest(
        Guid PurchaseId,
        string ErrorMessage);

    public record CatalogSupplierItem(
        Guid Id,
        string Name,
        string? Phone,
        string? Address,
        decimal Balance,
        string? Email = null,
        string? ContactPerson = null);

    public record CatalogCategoryItem(
        Guid Id,
        string NameAr,
        string? NameEn = null);

    public record CatalogProductItem(
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

    public record PushCatalogRequest(
        List<CatalogSupplierItem>? Suppliers,
        List<CatalogProductItem>? Products,
        List<CatalogCategoryItem>? Categories = null);

    public record CloudStoreSettingsDto(
        string StoreName,
        string? Address,
        string? Phone,
        decimal TaxRate,
        bool IsTaxIncluded,
        string Currency,
        string? InvoiceFooterMessage,
        bool AllowNegativeStock,
        string? LogoUrl);

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

    public record SyncResultDto(
        bool Success,
        int ProcessedCount,
        string Message);

    // Expenses Sync DTOs
    public record CloudExpenseSyncDto(
        Guid Id,
        string Title,
        decimal Amount,
        string? Category,
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

    public record NotificationActionRequest(
        string ActionType, // "OK", "Waste", "SupplierReplacement"
        decimal Quantity = 0,
        string? Reason = null,
        DateTime? NewExpiryDate = null,
        string? NewBatchNumber = null,
        string? Notes = null);

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

    public record CloudShiftSummaryDto(
        Guid Id,
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
        string? ClosingNotes,
        bool IsReadByOwner,
        DateTime CreatedAt);

    public record CloudReturnItemDto(
        Guid Id,
        Guid ProductId,
        string? ProductName,
        string? Barcode,
        decimal Quantity,
        decimal UnitPrice,
        decimal Tax,
        decimal Total,
        string? Reason);

    public record CloudReturnDto(
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
        int ItemsCount,
        DateTime CreatedAt);

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
}
