using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.CloudAPI.Entities
{
    public enum SyncStatus
    {
        PendingSync = 1,
        Synced = 2,
        SyncFailed = 3
    }

    public enum PurchasePaymentMethod
    {
        Cash = 1,
        Card = 2,
        MobileWallet = 3,
        Credit = 4
    }

    public class Tenant
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(150)]
        public string Name { get; set; } = default!;

        [Required, MaxLength(50)]
        public string Code { get; set; } = default!;

        [MaxLength(200)]
        public string? SyncApiKey { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CloudUser
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        [Required, MaxLength(100)]
        public string Username { get; set; } = default!;

        [Required, MaxLength(255)]
        public string PasswordHash { get; set; } = default!;

        [Required, MaxLength(150)]
        public string FullName { get; set; } = default!;

        [MaxLength(50)]
        public string Role { get; set; } = "Owner";

        [MaxLength(50)]
        public string? Phone { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CloudSupplier
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = default!;

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(250)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }

        [MaxLength(100)]
        public string? ContactPerson { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; }

        public bool IsActive { get; set; } = true;

        public SyncStatus SyncStatus { get; set; } = SyncStatus.Synced;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    public class CloudProduct
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        [Required, MaxLength(100)]
        public string Barcode { get; set; } = default!;

        [Required, MaxLength(200)]
        public string NameAr { get; set; } = default!;

        [MaxLength(200)]
        public string? NameEn { get; set; }

        [MaxLength(50)]
        public string BaseUnit { get; set; } = "قطعة";

        [MaxLength(50)]
        public string? ParentUnit { get; set; } = "كرتونة";

        public int ConversionFactor { get; set; } = 1;

        [Column(TypeName = "decimal(18,2)")]
        public decimal PurchasePrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SellingPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal WholesalePrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal StockQuantity { get; set; }

        public Guid? CategoryId { get; set; }

        [MaxLength(100)]
        public string? CategoryName { get; set; }

        public bool IsWeighable { get; set; }
        public int ShelfLifeDays { get; set; }
        public int ExpiryAlertDays { get; set; } = 3;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ReorderLevel { get; set; }

        public bool TrackExpiry { get; set; }
        public bool IsActive { get; set; } = true;

        public SyncStatus SyncStatus { get; set; } = SyncStatus.Synced;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CloudCategory
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        [Required, MaxLength(150)]
        public string NameAr { get; set; } = default!;

        [MaxLength(150)]
        public string? NameEn { get; set; }

        public bool IsActive { get; set; } = true;

        public SyncStatus SyncStatus { get; set; } = SyncStatus.Synced;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    public class CloudDebtItem
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        [Required, MaxLength(20)]
        public string Type { get; set; } = "Customer"; // "Customer" or "Supplier"

        public Guid ReferenceId { get; set; } // SaleId or PurchaseId

        [Required, MaxLength(100)]
        public string InvoiceNumber { get; set; } = default!;

        [Required, MaxLength(150)]
        public string EntityName { get; set; } = default!;

        [MaxLength(50)]
        public string? Phone { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingAmount { get; set; }

        public DateTime Date { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CloudDebtPayment
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        [Required, MaxLength(20)]
        public string DebtType { get; set; } = "Supplier"; // "Customer" or "Supplier"

        public Guid ReferenceId { get; set; } // SaleId or PurchaseId

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public SyncStatus SyncStatus { get; set; } = SyncStatus.PendingSync;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CloudExpense
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = default!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(100)]
        public string? Category { get; set; }

        public DateTime Date { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? Notes { get; set; }

        public SyncStatus SyncStatus { get; set; } = SyncStatus.Synced;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CloudDashboardSnapshot
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TodaySales { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TodayProfit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TodayPurchases { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TodayExpenses { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthSales { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthProfit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthPurchases { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthExpenses { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CustomerDebtsTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SupplierDebtsTotal { get; set; }

        public int LowStockCount { get; set; }
        public int ExpiryAlertsCount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TodayWasteLoss { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthWasteLoss { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalWasteLoss { get; set; }

        public string? MonthlySalesJson { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CloudPurchase
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid(); // Unique Guid (Idempotency Key)

        public Guid TenantId { get; set; }

        [Required, MaxLength(100)]
        public string InvoiceNumber { get; set; } = default!;

        [MaxLength(100)]
        public string? InternalNumber { get; set; }

        public Guid SupplierId { get; set; }

        [MaxLength(150)]
        public string? SupplierName { get; set; }

        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingAmount { get; set; }

        public PurchasePaymentMethod PaymentMethod { get; set; } = PurchasePaymentMethod.Cash;

        [MaxLength(500)]
        public string? Notes { get; set; }

        public Guid CreatedByUserId { get; set; }

        [MaxLength(100)]
        public string? CreatedByName { get; set; }

        // Sync Metadata
        public SyncStatus SyncStatus { get; set; } = SyncStatus.PendingSync;

        public int SyncAttempts { get; set; } = 0;

        [MaxLength(1000)]
        public string? SyncError { get; set; }

        public DateTime? SyncedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<CloudPurchaseItem> Items { get; set; } = new();
    }

    public class CloudPurchaseItem
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid PurchaseId { get; set; }

        public Guid ProductId { get; set; }

        [Required, MaxLength(200)]
        public string ProductName { get; set; } = default!;

        [MaxLength(100)]
        public string? Barcode { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Tax { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        public DateTime? ExpiryDate { get; set; }

        [MaxLength(100)]
        public string? BatchNumber { get; set; }

        [MaxLength(50)]
        public string? Unit { get; set; }
    }

    public class SyncRecord
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        [Required, MaxLength(50)]
        public string EntityType { get; set; } = "Purchase"; // Purchase, Product, Supplier

        public Guid EntityId { get; set; }

        [Required, MaxLength(50)]
        public string Direction { get; set; } = "CloudToLocal"; // CloudToLocal, LocalToCloud

        [Required, MaxLength(50)]
        public string Status { get; set; } = "Success"; // Success, Failed

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [MaxLength(1000)]
        public string? Details { get; set; }
    }

    public class CloudStoreSettings
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        [Required, MaxLength(200)]
        public string StoreName { get; set; } = "المتجر الرئيسي";

        [MaxLength(500)]
        public string? Address { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxRate { get; set; }

        public bool IsTaxIncluded { get; set; }

        [MaxLength(20)]
        public string Currency { get; set; } = "ج.م";

        [MaxLength(500)]
        public string? InvoiceFooterMessage { get; set; }

        public bool AllowNegativeStock { get; set; }

        public string? LogoUrl { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CloudExpiryNotification
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        public Guid ProductId { get; set; }

        [Required, MaxLength(200)]
        public string ProductName { get; set; } = default!;

        [MaxLength(100)]
        public string? Barcode { get; set; }

        public Guid? BatchId { get; set; }

        [MaxLength(100)]
        public string? BatchNumber { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingQuantity { get; set; }

        [MaxLength(50)]
        public string Unit { get; set; } = "قطعة";

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitCost { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public int DaysRemaining { get; set; }

        public bool IsExpired { get; set; }

        [MaxLength(500)]
        public string Message { get; set; } = default!;

        [MaxLength(50)]
        public string Status { get; set; } = "Active"; // "Active", "Resolved", "Snoozed"

        [MaxLength(50)]
        public string? ActionType { get; set; } // "OK", "Waste", "SupplierReplacement"

        [Column(TypeName = "decimal(18,2)")]
        public decimal ActionQuantity { get; set; }

        [MaxLength(200)]
        public string? ActionReason { get; set; }

        public DateTime? NewExpiryDate { get; set; }

        [MaxLength(100)]
        public string? NewBatchNumber { get; set; }

        [MaxLength(500)]
        public string? ActionNotes { get; set; }

        public SyncStatus SyncStatus { get; set; } = SyncStatus.Synced;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ActionTakenAt { get; set; }
    }

    public class CloudShiftSummary
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        public Guid ShiftId { get; set; }

        [Required, MaxLength(150)]
        public string CashierName { get; set; } = default!;

        public DateTime OpenedAt { get; set; }

        public DateTime ClosedAt { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OpeningCash { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ActualClosingCash { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SystemCash { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CashDifference { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalSales { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCash { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCard { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalWallet { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCredit { get; set; }

        public int TotalInvoices { get; set; }

        public int TotalReturns { get; set; }

        [MaxLength(500)]
        public string? ClosingNotes { get; set; }

        public bool IsReadByOwner { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CloudReturn
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TenantId { get; set; }

        [Required, MaxLength(100)]
        public string ReturnNumber { get; set; } = default!;

        [Required, MaxLength(50)]
        public string Type { get; set; } = "Sales"; // "Sales" or "Purchase"

        [MaxLength(100)]
        public string? OriginalInvoiceNumber { get; set; }

        [MaxLength(200)]
        public string? PartyName { get; set; } // CustomerName or SupplierName

        [MaxLength(50)]
        public string? PartyPhone { get; set; }

        public DateTime ReturnDate { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [MaxLength(50)]
        public string RefundMethod { get; set; } = "نقداً";

        [MaxLength(250)]
        public string? Reason { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public string? ItemsJson { get; set; }

        public int ItemsCount { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
