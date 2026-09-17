using Microsoft.EntityFrameworkCore;
using POS.CloudAPI.Entities;
using System.Security.Cryptography;
using System.Text;

namespace POS.CloudAPI.Database
{
    public class CloudDbContext : DbContext
    {
        public CloudDbContext(DbContextOptions<CloudDbContext> options) : base(options)
        {
        }

        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<CloudUser> Users => Set<CloudUser>();
        public DbSet<CloudSupplier> Suppliers => Set<CloudSupplier>();
        public DbSet<CloudProduct> Products => Set<CloudProduct>();
        public DbSet<CloudCategory> Categories => Set<CloudCategory>();
        public DbSet<CloudPurchase> Purchases => Set<CloudPurchase>();
        public DbSet<CloudPurchaseItem> PurchaseItems => Set<CloudPurchaseItem>();
        public DbSet<CloudDebtItem> DebtItems => Set<CloudDebtItem>();
        public DbSet<CloudDebtPayment> DebtPayments => Set<CloudDebtPayment>();
        public DbSet<CloudExpense> Expenses => Set<CloudExpense>();
        public DbSet<CloudDashboardSnapshot> DashboardSnapshots => Set<CloudDashboardSnapshot>();
        public DbSet<CloudStoreSettings> StoreSettings => Set<CloudStoreSettings>();
        public DbSet<SyncRecord> SyncRecords => Set<SyncRecord>();
        public DbSet<CloudExpiryNotification> ExpiryNotifications => Set<CloudExpiryNotification>();
        public DbSet<CloudShiftSummary> ShiftSummaries => Set<CloudShiftSummary>();
        public DbSet<CloudReturn> Returns => Set<CloudReturn>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Tenant>(b =>
            {
                b.HasIndex(t => t.Code).IsUnique();
            });

            modelBuilder.Entity<CloudUser>(b =>
            {
                b.HasIndex(u => new { u.TenantId, u.Username }).IsUnique();
            });

            modelBuilder.Entity<CloudSupplier>(b =>
            {
                b.HasIndex(s => new { s.TenantId, s.Name });
            });

            modelBuilder.Entity<CloudCategory>(b =>
            {
                b.HasIndex(c => new { c.TenantId, c.NameAr });
            });

            modelBuilder.Entity<CloudProduct>(b =>
            {
                b.HasIndex(p => new { p.TenantId, p.Barcode });
                b.HasIndex(p => new { p.TenantId, p.NameAr });
                b.HasIndex(p => new { p.TenantId, p.SyncStatus });
            });

            modelBuilder.Entity<CloudPurchase>(b =>
            {
                b.HasIndex(p => new { p.TenantId, p.InvoiceNumber });
                b.HasIndex(p => new { p.TenantId, p.SyncStatus });
                b.HasIndex(p => new { p.TenantId, p.CreatedAt });
                b.HasIndex(p => new { p.TenantId, p.SyncStatus, p.CreatedAt });
                b.HasMany(p => p.Items)
                 .WithOne()
                 .HasForeignKey(i => i.PurchaseId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CloudDebtItem>(b =>
            {
                b.HasIndex(d => new { d.TenantId, d.Type, d.ReferenceId });
            });

            modelBuilder.Entity<CloudDebtPayment>(b =>
            {
                b.HasIndex(p => new { p.TenantId, p.SyncStatus });
            });

            modelBuilder.Entity<CloudExpense>(b =>
            {
                b.HasIndex(e => new { e.TenantId, e.Date });
            });

            modelBuilder.Entity<CloudDashboardSnapshot>(b =>
            {
                b.HasIndex(s => s.TenantId);
            });

            modelBuilder.Entity<SyncRecord>(b =>
            {
                b.HasIndex(r => new { r.TenantId, r.EntityType, r.EntityId });
            });

            modelBuilder.Entity<CloudExpiryNotification>(b =>
            {
                b.HasIndex(n => new { n.TenantId, n.Status });
                b.HasIndex(n => new { n.TenantId, n.SyncStatus });
            });

            modelBuilder.Entity<CloudShiftSummary>(b =>
            {
                b.HasIndex(s => new { s.TenantId, s.ClosedAt });
                b.HasIndex(s => new { s.TenantId, s.ShiftId });
            });

            modelBuilder.Entity<CloudReturn>(b =>
            {
                b.HasIndex(r => new { r.TenantId, r.Type });
                b.HasIndex(r => new { r.TenantId, r.ReturnDate });
                b.HasIndex(r => new { r.TenantId, r.ReturnNumber });
            });
        }

        public async Task SeedInitialDataAsync()
        {
            if (!await Tenants.AnyAsync())
            {
                var defaultTenant = new Tenant
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name = "سوبر ماركت",
                    Code = "SHOP01",
                    SyncApiKey = "KEY-SHOP01-SECURE-SYNC-2026",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                Tenants.Add(defaultTenant);

                // Default Owner User: admin / 123456
                var defaultUser = new CloudUser
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    TenantId = defaultTenant.Id,
                    Username = "admin",
                    PasswordHash = HashPassword("123456"),
                    FullName = "صاحب المحل",
                    Role = "Owner",
                    Phone = "",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                Users.Add(defaultUser);

                await SaveChangesAsync();
            }

            // Clean up any old sample mock data so client starts with a clean system
            var sampleBarcodes = new[] { "6221001001", "6221001002", "6221001003" };
            var sampleProducts = await Products.Where(p => sampleBarcodes.Contains(p.Barcode)).ToListAsync();
            if (sampleProducts.Any())
            {
                Products.RemoveRange(sampleProducts);
            }

            var sampleSupplierIds = new[]
            {
                Guid.Parse("33333333-3333-3333-3333-333333333331"),
                Guid.Parse("33333333-3333-3333-3333-333333333332"),
                Guid.Parse("33333333-3333-3333-3333-333333333333")
            };
            var sampleSuppliers = await Suppliers.Where(s => sampleSupplierIds.Contains(s.Id)).ToListAsync();
            if (sampleSuppliers.Any())
            {
                Suppliers.RemoveRange(sampleSuppliers);
            }

            await SaveChangesAsync();
        }

        public async Task EnsureSchemaUpToDateAsync()
        {
            try
            {
                var sql = @"
-- Products columns
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'CategoryId')
    ALTER TABLE [Products] ADD [CategoryId] UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'CategoryName')
    ALTER TABLE [Products] ADD [CategoryName] NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'IsWeighable')
    ALTER TABLE [Products] ADD [IsWeighable] BIT NOT NULL CONSTRAINT DF_Products_IsWeighable DEFAULT(0);

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ShelfLifeDays')
    ALTER TABLE [Products] ADD [ShelfLifeDays] INT NOT NULL CONSTRAINT DF_Products_ShelfLifeDays DEFAULT(0);

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ExpiryAlertDays')
    ALTER TABLE [Products] ADD [ExpiryAlertDays] INT NOT NULL CONSTRAINT DF_Products_ExpiryAlertDays DEFAULT(3);

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ReorderLevel')
    ALTER TABLE [Products] ADD [ReorderLevel] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Products_ReorderLevel DEFAULT(0);

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'SyncStatus')
    ALTER TABLE [Products] ADD [SyncStatus] INT NOT NULL CONSTRAINT DF_Products_SyncStatus DEFAULT(2);

-- Suppliers columns
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Suppliers') AND name = 'UpdatedAt')
    ALTER TABLE [Suppliers] ADD [UpdatedAt] DATETIME2 NULL;

-- Purchases columns
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Purchases') AND name = 'InternalNumber')
    ALTER TABLE [Purchases] ADD [InternalNumber] NVARCHAR(100) NULL;

-- Categories table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Categories')
BEGIN
    CREATE TABLE [Categories] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [NameAr] NVARCHAR(150) NOT NULL,
        [NameEn] NVARCHAR(150) NULL,
        [IsActive] BIT NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT 1,
        [SyncStatus] INT NOT NULL CONSTRAINT DF_Categories_SyncStatus DEFAULT 2,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Categories_CreatedAt DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NULL
    );
    CREATE INDEX [IX_Categories_TenantId_NameAr] ON [Categories] ([TenantId], [NameAr]);
END
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'UpdatedAt')
    ALTER TABLE [Categories] ADD [UpdatedAt] DATETIME2 NULL;

-- DashboardSnapshots table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DashboardSnapshots')
BEGIN
    CREATE TABLE [DashboardSnapshots] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [TodaySales] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Dashboard_TodaySales DEFAULT 0,
        [TodayProfit] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Dashboard_TodayProfit DEFAULT 0,
        [TodayPurchases] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Dashboard_TodayPurchases DEFAULT 0,
        [TodayExpenses] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Dashboard_TodayExpenses DEFAULT 0,
        [MonthSales] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Dashboard_MonthSales DEFAULT 0,
        [MonthProfit] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Dashboard_MonthProfit DEFAULT 0,
        [MonthPurchases] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Dashboard_MonthPurchases DEFAULT 0,
        [MonthExpenses] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Dashboard_MonthExpenses DEFAULT 0,
        [CustomerDebtsTotal] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Dashboard_CustomerDebtsTotal DEFAULT 0,
        [SupplierDebtsTotal] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Dashboard_SupplierDebtsTotal DEFAULT 0,
        [LowStockCount] INT NOT NULL CONSTRAINT DF_Dashboard_LowStockCount DEFAULT 0,
        [ExpiryAlertsCount] INT NOT NULL CONSTRAINT DF_Dashboard_ExpiryAlertsCount DEFAULT 0,
        [MonthlySalesJson] NVARCHAR(MAX) NULL,
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Dashboard_UpdatedAt DEFAULT GETUTCDATE()
    );
    CREATE INDEX [IX_DashboardSnapshots_TenantId] ON [DashboardSnapshots] ([TenantId]);
END

-- DebtItems table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DebtItems')
BEGIN
    CREATE TABLE [DebtItems] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [Type] NVARCHAR(20) NOT NULL,
        [ReferenceId] UNIQUEIDENTIFIER NOT NULL,
        [InvoiceNumber] NVARCHAR(100) NOT NULL,
        [EntityName] NVARCHAR(150) NOT NULL,
        [Phone] NVARCHAR(50) NULL,
        [TotalAmount] DECIMAL(18,2) NOT NULL CONSTRAINT DF_DebtItems_TotalAmount DEFAULT 0,
        [PaidAmount] DECIMAL(18,2) NOT NULL CONSTRAINT DF_DebtItems_PaidAmount DEFAULT 0,
        [RemainingAmount] DECIMAL(18,2) NOT NULL CONSTRAINT DF_DebtItems_RemainingAmount DEFAULT 0,
        [Date] DATETIME2 NOT NULL CONSTRAINT DF_DebtItems_Date DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_DebtItems_UpdatedAt DEFAULT GETUTCDATE()
    );
    CREATE INDEX [IX_DebtItems_TenantId_Type_ReferenceId] ON [DebtItems] ([TenantId], [Type], [ReferenceId]);
END

-- DebtPayments table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DebtPayments')
BEGIN
    CREATE TABLE [DebtPayments] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [DebtType] NVARCHAR(20) NOT NULL,
        [ReferenceId] UNIQUEIDENTIFIER NOT NULL,
        [Amount] DECIMAL(18,2) NOT NULL CONSTRAINT DF_DebtPayments_Amount DEFAULT 0,
        [Notes] NVARCHAR(500) NULL,
        [SyncStatus] INT NOT NULL CONSTRAINT DF_DebtPayments_SyncStatus DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_DebtPayments_CreatedAt DEFAULT GETUTCDATE()
    );
    CREATE INDEX [IX_DebtPayments_TenantId_SyncStatus] ON [DebtPayments] ([TenantId], [SyncStatus]);
END

-- Expenses table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Expenses')
BEGIN
    CREATE TABLE [Expenses] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [Amount] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Expenses_Amount DEFAULT 0,
        [Category] NVARCHAR(100) NULL,
        [Date] DATETIME2 NOT NULL CONSTRAINT DF_Expenses_Date DEFAULT GETUTCDATE(),
        [Notes] NVARCHAR(500) NULL,
        [SyncStatus] INT NOT NULL CONSTRAINT DF_Expenses_SyncStatus DEFAULT 2,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Expenses_CreatedAt DEFAULT GETUTCDATE()
    );
    CREATE INDEX [IX_Expenses_TenantId_Date] ON [Expenses] ([TenantId], [Date]);
END

-- Suppliers table enhancements
IF COL_LENGTH('Suppliers', 'Email') IS NULL
    ALTER TABLE [Suppliers] ADD [Email] NVARCHAR(100) NULL;

IF COL_LENGTH('Suppliers', 'ContactPerson') IS NULL
    ALTER TABLE [Suppliers] ADD [ContactPerson] NVARCHAR(100) NULL;

IF COL_LENGTH('Suppliers', 'SyncStatus') IS NULL
    ALTER TABLE [Suppliers] ADD [SyncStatus] INT NOT NULL CONSTRAINT DF_Suppliers_SyncStatus DEFAULT 2;

-- StoreSettings table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StoreSettings')
BEGIN
    CREATE TABLE [StoreSettings] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [StoreName] NVARCHAR(200) NOT NULL CONSTRAINT DF_StoreSettings_StoreName DEFAULT N'المتجر الرئيسي',
        [Address] NVARCHAR(500) NULL,
        [Phone] NVARCHAR(50) NULL,
        [TaxRate] DECIMAL(18,2) NOT NULL CONSTRAINT DF_StoreSettings_TaxRate DEFAULT 0,
        [IsTaxIncluded] BIT NOT NULL CONSTRAINT DF_StoreSettings_IsTaxIncluded DEFAULT 0,
        [Currency] NVARCHAR(20) NOT NULL CONSTRAINT DF_StoreSettings_Currency DEFAULT N'ج.م',
        [InvoiceFooterMessage] NVARCHAR(500) NULL,
        [AllowNegativeStock] BIT NOT NULL CONSTRAINT DF_StoreSettings_AllowNegativeStock DEFAULT 0,
        [LogoUrl] NVARCHAR(MAX) NULL,
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT DF_StoreSettings_UpdatedAt DEFAULT GETUTCDATE()
    );
    CREATE UNIQUE INDEX [IX_StoreSettings_TenantId] ON [StoreSettings] ([TenantId]);
END

-- DashboardSnapshots columns for waste loss
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardSnapshots') AND name = 'TodayWasteLoss')
    ALTER TABLE [DashboardSnapshots] ADD [TodayWasteLoss] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Dashboard_TodayWasteLoss DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardSnapshots') AND name = 'MonthWasteLoss')
    ALTER TABLE [DashboardSnapshots] ADD [MonthWasteLoss] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Dashboard_MonthWasteLoss DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DashboardSnapshots') AND name = 'TotalWasteLoss')
    ALTER TABLE [DashboardSnapshots] ADD [TotalWasteLoss] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Dashboard_TotalWasteLoss DEFAULT 0;

-- ExpiryNotifications table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ExpiryNotifications')
BEGIN
    CREATE TABLE [ExpiryNotifications] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [ProductId] UNIQUEIDENTIFIER NOT NULL,
        [ProductName] NVARCHAR(200) NOT NULL,
        [Barcode] NVARCHAR(100) NULL,
        [BatchId] UNIQUEIDENTIFIER NULL,
        [BatchNumber] NVARCHAR(100) NULL,
        [RemainingQuantity] DECIMAL(18,2) NOT NULL CONSTRAINT DF_ExpiryNotifications_RemainingQuantity DEFAULT 0,
        [Unit] NVARCHAR(50) NOT NULL CONSTRAINT DF_ExpiryNotifications_Unit DEFAULT N'قطعة',
        [UnitCost] DECIMAL(18,2) NOT NULL CONSTRAINT DF_ExpiryNotifications_UnitCost DEFAULT 0,
        [ExpiryDate] DATETIME2 NULL,
        [DaysRemaining] INT NOT NULL CONSTRAINT DF_ExpiryNotifications_DaysRemaining DEFAULT 0,
        [IsExpired] BIT NOT NULL CONSTRAINT DF_ExpiryNotifications_IsExpired DEFAULT 0,
        [Message] NVARCHAR(500) NOT NULL,
        [Status] NVARCHAR(50) NOT NULL CONSTRAINT DF_ExpiryNotifications_Status DEFAULT N'Active',
        [ActionType] NVARCHAR(50) NULL,
        [ActionQuantity] DECIMAL(18,2) NOT NULL CONSTRAINT DF_ExpiryNotifications_ActionQuantity DEFAULT 0,
        [ActionReason] NVARCHAR(200) NULL,
        [NewExpiryDate] DATETIME2 NULL,
        [NewBatchNumber] NVARCHAR(100) NULL,
        [ActionNotes] NVARCHAR(500) NULL,
        [SyncStatus] INT NOT NULL CONSTRAINT DF_ExpiryNotifications_SyncStatus DEFAULT 2,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_ExpiryNotifications_CreatedAt DEFAULT GETUTCDATE(),
        [ActionTakenAt] DATETIME2 NULL
    );
    CREATE INDEX [IX_ExpiryNotifications_TenantId_Status] ON [ExpiryNotifications] ([TenantId], [Status]);
    CREATE INDEX [IX_ExpiryNotifications_TenantId_SyncStatus] ON [ExpiryNotifications] ([TenantId], [SyncStatus]);
END

-- ShiftSummaries table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ShiftSummaries')
BEGIN
    CREATE TABLE [ShiftSummaries] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [ShiftId] UNIQUEIDENTIFIER NOT NULL,
        [CashierName] NVARCHAR(150) NOT NULL,
        [OpenedAt] DATETIME2 NOT NULL,
        [ClosedAt] DATETIME2 NOT NULL,
        [OpeningCash] DECIMAL(18,2) NOT NULL CONSTRAINT DF_ShiftSummaries_OpeningCash DEFAULT 0,
        [ActualClosingCash] DECIMAL(18,2) NOT NULL CONSTRAINT DF_ShiftSummaries_ActualClosingCash DEFAULT 0,
        [SystemCash] DECIMAL(18,2) NOT NULL CONSTRAINT DF_ShiftSummaries_SystemCash DEFAULT 0,
        [CashDifference] DECIMAL(18,2) NOT NULL CONSTRAINT DF_ShiftSummaries_CashDifference DEFAULT 0,
        [TotalSales] DECIMAL(18,2) NOT NULL CONSTRAINT DF_ShiftSummaries_TotalSales DEFAULT 0,
        [TotalCash] DECIMAL(18,2) NOT NULL CONSTRAINT DF_ShiftSummaries_TotalCash DEFAULT 0,
        [TotalCard] DECIMAL(18,2) NOT NULL CONSTRAINT DF_ShiftSummaries_TotalCard DEFAULT 0,
        [TotalWallet] DECIMAL(18,2) NOT NULL CONSTRAINT DF_ShiftSummaries_TotalWallet DEFAULT 0,
        [TotalCredit] DECIMAL(18,2) NOT NULL CONSTRAINT DF_ShiftSummaries_TotalCredit DEFAULT 0,
        [TotalInvoices] INT NOT NULL CONSTRAINT DF_ShiftSummaries_TotalInvoices DEFAULT 0,
        [TotalReturns] INT NOT NULL CONSTRAINT DF_ShiftSummaries_TotalReturns DEFAULT 0,
        [ClosingNotes] NVARCHAR(500) NULL,
        [IsReadByOwner] BIT NOT NULL CONSTRAINT DF_ShiftSummaries_IsReadByOwner DEFAULT 0,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_ShiftSummaries_CreatedAt DEFAULT GETUTCDATE()
    );
    CREATE INDEX [IX_ShiftSummaries_TenantId_ClosedAt] ON [ShiftSummaries] ([TenantId], [ClosedAt]);
    CREATE INDEX [IX_ShiftSummaries_TenantId_ShiftId] ON [ShiftSummaries] ([TenantId], [ShiftId]);
END

-- SyncRecords table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SyncRecords')
BEGIN
    CREATE TABLE [SyncRecords] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [EntityType] NVARCHAR(50) NOT NULL,
        [EntityId] UNIQUEIDENTIFIER NOT NULL,
        [Direction] NVARCHAR(50) NOT NULL,
        [Status] NVARCHAR(50) NOT NULL,
        [Timestamp] DATETIME2 NOT NULL CONSTRAINT DF_SyncRecords_Timestamp DEFAULT GETUTCDATE(),
        [Details] NVARCHAR(1000) NULL
    );
    CREATE INDEX [IX_SyncRecords_TenantId_EntityType_EntityId] ON [SyncRecords] ([TenantId], [EntityType], [EntityId]);
END

-- Returns table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Returns')
BEGIN
    CREATE TABLE [Returns] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [ReturnNumber] NVARCHAR(100) NOT NULL,
        [Type] NVARCHAR(50) NOT NULL,
        [OriginalInvoiceNumber] NVARCHAR(100) NULL,
        [PartyName] NVARCHAR(200) NULL,
        [PartyPhone] NVARCHAR(50) NULL,
        [ReturnDate] DATETIME2 NOT NULL CONSTRAINT DF_Returns_ReturnDate DEFAULT GETUTCDATE(),
        [TotalAmount] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Returns_TotalAmount DEFAULT 0,
        [RefundMethod] NVARCHAR(50) NOT NULL CONSTRAINT DF_Returns_RefundMethod DEFAULT N'نقداً',
        [Reason] NVARCHAR(250) NULL,
        [Notes] NVARCHAR(500) NULL,
        [ItemsJson] NVARCHAR(MAX) NULL,
        [ItemsCount] INT NOT NULL CONSTRAINT DF_Returns_ItemsCount DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT DF_Returns_CreatedAt DEFAULT GETUTCDATE()
    );
    CREATE INDEX [IX_Returns_TenantId_Type] ON [Returns] ([TenantId], [Type]);
    CREATE INDEX [IX_Returns_TenantId_ReturnDate] ON [Returns] ([TenantId], [ReturnDate]);
    CREATE INDEX [IX_Returns_TenantId_ReturnNumber] ON [Returns] ([TenantId], [ReturnNumber]);
END
";
                await Database.ExecuteSqlRawAsync(sql);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Schema Upgrade Notice] {ex.Message}");
            }
        }

        public static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        public static bool VerifyPassword(string password, string storedHash)
        {
            return HashPassword(password) == storedHash;
        }
    }
}
