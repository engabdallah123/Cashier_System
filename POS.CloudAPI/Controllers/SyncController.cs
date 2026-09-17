using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.CloudAPI.Database;
using POS.CloudAPI.DTOs;
using POS.CloudAPI.Entities;

namespace POS.CloudAPI.Controllers
{
    [ApiController]
    [Route("api/sync")]
    public class SyncController : ControllerBase
    {
        private readonly CloudDbContext _db;
        private readonly ILogger<SyncController> _logger;

        public SyncController(CloudDbContext db, ILogger<SyncController> logger)
        {
            _db = db;
            _logger = logger;
        }

        private async Task<Tenant?> AuthenticateSyncClientAsync()
        {
            var apiKey = Request.Headers["X-Sync-ApiKey"].FirstOrDefault();
            var shopCode = Request.Headers["X-Shop-Code"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                return await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.SyncApiKey == apiKey && t.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(shopCode))
            {
                return await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Code == shopCode && t.IsActive);
            }

            return await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.IsActive);
        }

        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new
            {
                status = "Online",
                serverTime = DateTime.UtcNow,
                version = "1.1.0"
            });
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetSyncStatus()
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            bool hasPendingPurchases = await _db.Purchases.AnyAsync(p => p.TenantId == tenant.Id && p.SyncStatus == SyncStatus.PendingSync);
            bool hasPendingSuppliers = await _db.Suppliers.AnyAsync(s => s.TenantId == tenant.Id && s.SyncStatus == SyncStatus.PendingSync);
            bool hasPendingDebts = await _db.DebtPayments.AnyAsync(d => d.TenantId == tenant.Id && d.SyncStatus == SyncStatus.PendingSync);
            bool hasPendingNotificationActions = await _db.ExpiryNotifications.AnyAsync(n => n.TenantId == tenant.Id && n.ActionType != null && n.SyncStatus == SyncStatus.PendingSync);
            bool hasPendingCategories = await _db.Categories.AnyAsync(c => c.TenantId == tenant.Id && c.SyncStatus == SyncStatus.PendingSync);

            bool hasPending = hasPendingPurchases || hasPendingSuppliers || hasPendingDebts || hasPendingNotificationActions || hasPendingCategories;

            return Ok(new CloudSyncStatusDto(
                HasPending: hasPending,
                HasPendingPurchases: hasPendingPurchases,
                HasPendingSuppliers: hasPendingSuppliers,
                HasPendingDebts: hasPendingDebts,
                HasPendingNotificationActions: hasPendingNotificationActions,
                HasPendingCategories: hasPendingCategories,
                ServerTime: DateTime.UtcNow
            ));
        }

        // ==================== 1. PURCHASES SYNC ====================

        [HttpGet("purchases/pending")]
        public async Task<IActionResult> GetPendingPurchases()
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null)
                return Unauthorized(new { message = "غير مصرح لبرنامج الكاشير بالاتصال بالسحابة." });

            var pendingPurchases = await _db.Purchases
                .AsNoTracking()
                .Include(p => p.Items)
                .Where(p => p.TenantId == tenant.Id && p.SyncStatus == SyncStatus.PendingSync)
                .OrderBy(p => p.CreatedAt)
                .Take(50)
                .ToListAsync();

            var result = pendingPurchases.Select(p => new CloudPurchaseDetailDto(
                p.Id,
                p.InvoiceNumber,
                p.InternalNumber,
                p.SupplierId,
                p.SupplierName,
                p.PurchaseDate,
                p.SubTotal,
                p.DiscountAmount,
                p.TaxAmount,
                p.TotalAmount,
                p.PaidAmount,
                p.RemainingAmount,
                (int)p.PaymentMethod,
                p.Notes,
                p.CreatedByName,
                p.SyncStatus.ToString(),
                p.SyncedAt,
                p.SyncError,
                p.SyncAttempts,
                p.CreatedAt,
                p.Items.Select(i => new CloudPurchaseItemDto(
                    i.Id,
                    i.ProductId,
                    i.ProductName,
                    i.Barcode,
                    i.Quantity,
                    i.UnitCost,
                    i.Discount,
                    i.Tax,
                    i.Total,
                    i.ExpiryDate,
                    i.BatchNumber,
                    i.Unit
                )).ToList()
            )).ToList();

            return Ok(result);
        }

        [HttpPost("purchases/{id:guid}/acknowledge")]
        public async Task<IActionResult> AcknowledgePurchase(Guid id, [FromBody] AcknowledgeSyncRequest? req)
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            var purchase = await _db.Purchases.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenant.Id);
            if (purchase == null)
                return NotFound(new { message = "الفاتورة غير موجودة على السحابة." });

            purchase.SyncStatus = SyncStatus.Synced;
            purchase.SyncedAt = DateTime.UtcNow;
            purchase.SyncError = null;

            _db.SyncRecords.Add(new SyncRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                EntityType = "Purchase",
                EntityId = purchase.Id,
                Direction = "CloudToLocal",
                Status = "Success",
                Timestamp = DateTime.UtcNow,
                Details = $"تم استيراد الفاتورة رقم {purchase.InvoiceNumber} بنجاح إلى الكاشير المحلي. مرجع: {req?.LocalReference}"
            });

            await _db.SaveChangesAsync();
            return Ok(new { success = true, syncStatus = "Synced" });
        }

        [HttpPost("purchases/{id:guid}/fail")]
        public async Task<IActionResult> FailPurchase(Guid id, [FromBody] FailSyncRequest req)
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            var purchase = await _db.Purchases.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenant.Id);
            if (purchase == null) return NotFound();

            purchase.SyncStatus = SyncStatus.SyncFailed;
            purchase.SyncAttempts++;
            purchase.SyncError = req.ErrorMessage;

            _db.SyncRecords.Add(new SyncRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                EntityType = "Purchase",
                EntityId = purchase.Id,
                Direction = "CloudToLocal",
                Status = "Failed",
                Timestamp = DateTime.UtcNow,
                Details = $"فشل استيراد الفاتورة رقم {purchase.InvoiceNumber}: {req.ErrorMessage}"
            });

            await _db.SaveChangesAsync();
            return Ok(new { success = true, syncStatus = "SyncFailed" });
        }

        // ==================== 2. FAST STOCK & SUPPLIER BALANCES UPDATE ====================

        [HttpPost("stock/push")]
        public async Task<IActionResult> PushStock([FromBody] PushStockRequest req)
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            if (req.Items == null || !req.Items.Any())
                return Ok(new { count = 0 });

            var productIds = req.Items.Select(i => i.ProductId).ToList();
            var existingProducts = await _db.Products
                .Where(p => p.TenantId == tenant.Id && productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            int updated = 0;
            foreach (var item in req.Items)
            {
                if (existingProducts.TryGetValue(item.ProductId, out var product))
                {
                    if (product.StockQuantity != item.NewStockQuantity)
                    {
                        product.StockQuantity = item.NewStockQuantity;
                        product.UpdatedAt = DateTime.UtcNow;
                        updated++;
                    }
                }
            }

            if (updated > 0)
            {
                await _db.SaveChangesAsync();
            }

            return Ok(new { success = true, updatedCount = updated });
        }

        [HttpPost("suppliers/balances")]
        public async Task<IActionResult> PushSupplierBalances([FromBody] PushSupplierBalancesRequest req)
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            if (req.Items == null || !req.Items.Any())
                return Ok(new { count = 0 });

            var supplierIds = req.Items.Select(i => i.SupplierId).ToList();
            var existingSuppliers = await _db.Suppliers
                .Where(s => s.TenantId == tenant.Id && supplierIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id);

            int updated = 0;
            foreach (var item in req.Items)
            {
                if (existingSuppliers.TryGetValue(item.SupplierId, out var supplier))
                {
                    if (supplier.Balance != item.NewBalance)
                    {
                        supplier.Balance = item.NewBalance;
                        supplier.UpdatedAt = DateTime.UtcNow;
                        updated++;
                    }
                }
            }

            if (updated > 0)
            {
                await _db.SaveChangesAsync();
            }

            return Ok(new { success = true, updatedCount = updated });
        }

        // ==================== 3. DEBTS SYNC (PULL PAYMENTS & PUSH SNAPSHOTS) ====================

        [HttpGet("debts/pending")]
        public async Task<IActionResult> GetPendingDebtPayments()
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            var pendingPayments = await _db.DebtPayments
                .AsNoTracking()
                .Where(p => p.TenantId == tenant.Id && p.SyncStatus == SyncStatus.PendingSync)
                .OrderBy(p => p.CreatedAt)
                .Take(50)
                .ToListAsync();

            var result = pendingPayments.Select(p => new DebtPaymentSyncDto(
                p.Id,
                p.DebtType,
                p.ReferenceId,
                p.Amount,
                p.Notes,
                p.CreatedAt
            )).ToList();

            return Ok(result);
        }

        [HttpPost("debts/{id:guid}/acknowledge")]
        public async Task<IActionResult> AcknowledgeDebtPayment(Guid id)
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            var payment = await _db.DebtPayments.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenant.Id);
            if (payment == null) return NotFound();

            payment.SyncStatus = SyncStatus.Synced;
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [HttpPost("debts/push")]
        public async Task<IActionResult> PushDebts([FromBody] PushDebtsRequest req)
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            // 1. Fetch any pending debt payments recorded by mobile that haven't been acknowledged by desktop yet
            var pendingPayments = await _db.DebtPayments
                .Where(p => p.TenantId == tenant.Id && p.SyncStatus == SyncStatus.PendingSync)
                .ToListAsync();

            var pendingByRef = pendingPayments
                .GroupBy(p => (Type: (p.DebtType ?? "").ToLower(), p.ReferenceId))
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

            // Clear old snapshot
            var existingDebts = await _db.DebtItems.Where(d => d.TenantId == tenant.Id).ToListAsync();
            _db.DebtItems.RemoveRange(existingDebts);

            if (req.Debts != null && req.Debts.Any())
            {
                var newDebts = new List<CloudDebtItem>();

                foreach (var d in req.Debts)
                {
                    decimal pendingReduction = 0;
                    var key = (Type: (d.Type ?? "").ToLower(), d.ReferenceId);
                    if (pendingByRef.TryGetValue(key, out var pendingAmt))
                    {
                        pendingReduction = pendingAmt;
                    }

                    var effectivePaid = d.PaidAmount + pendingReduction;
                    var effectiveRemaining = Math.Max(0, d.RemainingAmount - pendingReduction);

                    // If the debt is still active (remaining > 0), keep it in cloud
                    if (effectiveRemaining > 0.001m)
                    {
                        newDebts.Add(new CloudDebtItem
                        {
                            Id = d.Id != Guid.Empty ? d.Id : Guid.NewGuid(),
                            TenantId = tenant.Id,
                            Type = d.Type ?? "Customer",
                            ReferenceId = d.ReferenceId,
                            InvoiceNumber = d.InvoiceNumber ?? "",
                            EntityName = d.EntityName ?? "عميل/مورد",
                            Phone = d.Phone,
                            TotalAmount = d.TotalAmount,
                            PaidAmount = effectivePaid,
                            RemainingAmount = effectiveRemaining,
                            Date = d.Date,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }

                if (newDebts.Any())
                {
                    _db.DebtItems.AddRange(newDebts);
                }
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true, count = req.Debts?.Count ?? 0 });
        }

        // ==================== 4. DASHBOARD SNAPSHOT PUSH ====================

        [HttpPost("dashboard/push")]
        public async Task<IActionResult> PushDashboardSnapshot([FromBody] PushDashboardSnapshotRequest req)
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            var snapshot = await _db.DashboardSnapshots.FirstOrDefaultAsync(s => s.TenantId == tenant.Id);
            if (snapshot == null)
            {
                snapshot = new CloudDashboardSnapshot
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id
                };
                _db.DashboardSnapshots.Add(snapshot);
            }

            snapshot.TodaySales = req.TodaySales;
            snapshot.TodayProfit = req.TodayProfit;
            snapshot.TodayPurchases = req.TodayPurchases;
            snapshot.TodayExpenses = req.TodayExpenses;
            snapshot.MonthSales = req.MonthSales;
            snapshot.MonthProfit = req.MonthProfit;
            snapshot.MonthPurchases = req.MonthPurchases;
            snapshot.MonthExpenses = req.MonthExpenses;
            snapshot.CustomerDebtsTotal = req.CustomerDebtsTotal;
            snapshot.SupplierDebtsTotal = req.SupplierDebtsTotal;
            snapshot.LowStockCount = req.LowStockCount;
            snapshot.ExpiryAlertsCount = req.ExpiryAlertsCount;
            snapshot.TodayWasteLoss = req.TodayWasteLoss;
            snapshot.MonthWasteLoss = req.MonthWasteLoss;
            snapshot.TotalWasteLoss = req.TotalWasteLoss;
            snapshot.MonthlySalesJson = req.MonthlySalesJson;
            snapshot.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ==================== 5. PENDING PRODUCTS PULL & ACK ====================

        [HttpGet("products/pending")]
        public async Task<IActionResult> GetPendingProducts()
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            var pending = await _db.Products
                .AsNoTracking()
                .Where(p => p.TenantId == tenant.Id && p.SyncStatus == SyncStatus.PendingSync)
                .Take(50)
                .ToListAsync();

            return Ok(pending.Select(p => new CloudProductDto(
                p.Id,
                p.Barcode,
                p.NameAr,
                p.NameEn,
                p.BaseUnit,
                p.ParentUnit,
                p.ConversionFactor,
                p.PurchasePrice,
                p.SellingPrice,
                p.WholesalePrice,
                p.StockQuantity,
                p.CategoryId,
                p.CategoryName,
                p.IsWeighable,
                p.ShelfLifeDays,
                p.ExpiryAlertDays,
                p.ReorderLevel,
                p.TrackExpiry,
                p.IsActive,
                p.SyncStatus.ToString()
            )));
        }

        [HttpPost("products/{id:guid}/acknowledge")]
        public async Task<IActionResult> AcknowledgeProduct(Guid id)
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            var prod = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenant.Id);
            if (prod == null) return NotFound();

            prod.SyncStatus = SyncStatus.Synced;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ==================== 5b. PENDING SUPPLIERS PULL & ACK ====================

        [HttpGet("suppliers/pending")]
        public async Task<IActionResult> GetPendingSuppliers()
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            var pending = await _db.Suppliers
                .AsNoTracking()
                .Where(s => s.TenantId == tenant.Id && s.SyncStatus == SyncStatus.PendingSync)
                .Take(50)
                .ToListAsync();

            return Ok(pending.Select(s => new PendingCloudSupplierDto(
                s.Id,
                s.Name,
                s.Phone ?? string.Empty,
                s.Email,
                s.Address,
                s.ContactPerson,
                s.Balance
            )));
        }

        [HttpPost("suppliers/{id:guid}/acknowledge")]
        public async Task<IActionResult> AcknowledgeSupplier(Guid id)
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            var sup = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenant.Id);
            if (sup == null) return NotFound();

            sup.SyncStatus = SyncStatus.Synced;
            sup.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ==================== 5b. CATEGORIES PENDING PULL ====================

        [HttpGet("categories/pending")]
        public async Task<IActionResult> GetPendingCategories()
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            var pending = await _db.Categories
                .AsNoTracking()
                .Where(c => c.TenantId == tenant.Id && c.SyncStatus == SyncStatus.PendingSync)
                .Take(50)
                .ToListAsync();

            return Ok(pending.Select(c => new PendingCloudCategoryDto(
                c.Id,
                c.NameAr,
                c.NameEn,
                c.IsActive,
                c.CreatedAt
            )));
        }

        [HttpPost("categories/{id:guid}/acknowledge")]
        public async Task<IActionResult> AcknowledgeCategory(Guid id)
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenant.Id);
            if (cat == null) return NotFound();

            cat.SyncStatus = SyncStatus.Synced;
            cat.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ==================== 5c. STORE SETTINGS & NAME PUSH ====================

        [HttpPost("store-settings/push")]
        public async Task<IActionResult> PushStoreSettings([FromBody] PushStoreSettingsRequest req)
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            var settings = await _db.StoreSettings.FirstOrDefaultAsync(s => s.TenantId == tenant.Id);
            if (settings == null)
            {
                settings = new CloudStoreSettings
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id
                };
                _db.StoreSettings.Add(settings);
            }

            settings.StoreName = req.StoreName;
            settings.Address = req.Address;
            settings.Phone = req.Phone;
            settings.TaxRate = req.TaxRate;
            settings.IsTaxIncluded = req.IsTaxIncluded;
            settings.Currency = string.IsNullOrWhiteSpace(req.Currency) ? "ج.م" : req.Currency;
            settings.InvoiceFooterMessage = req.InvoiceFooterMessage;
            settings.AllowNegativeStock = req.AllowNegativeStock;
            settings.LogoUrl = req.LogoUrl;
            settings.UpdatedAt = DateTime.UtcNow;

            tenant.Name = req.StoreName;

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ==================== 6. CATALOG FULL/INCREMENTAL PUSH ====================

        [HttpPost("catalog/push")]
        public async Task<IActionResult> PushCatalog([FromBody] PushCatalogRequest req)
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            int updatedSuppliers = 0;
            int updatedProducts = 0;

            // 1. Sync Suppliers
            if (req.Suppliers != null && req.Suppliers.Any())
            {
                var existingSuppliersList = await _db.Suppliers.Where(s => s.TenantId == tenant.Id).ToListAsync();
                var existingById = existingSuppliersList.ToDictionary(s => s.Id);
                var existingByName = existingSuppliersList
                    .GroupBy(s => s.Name.Trim().ToLower())
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var supReq in req.Suppliers)
                {
                    CloudSupplier? existing = null;
                    if (existingById.TryGetValue(supReq.Id, out var byId))
                    {
                        existing = byId;
                    }
                    else if (!string.IsNullOrWhiteSpace(supReq.Name) && existingByName.TryGetValue(supReq.Name.Trim().ToLower(), out var byName))
                    {
                        existing = byName;
                    }

                    if (existing != null)
                    {
                        bool changed = false;
                        if (existing.Name != supReq.Name) { existing.Name = supReq.Name; changed = true; }
                        if (existing.Phone != supReq.Phone) { existing.Phone = supReq.Phone; changed = true; }
                        if (existing.Address != supReq.Address) { existing.Address = supReq.Address; changed = true; }
                        if (existing.Balance != supReq.Balance) { existing.Balance = supReq.Balance; changed = true; }
                        if (existing.Email != supReq.Email) { existing.Email = supReq.Email; changed = true; }
                        if (existing.ContactPerson != supReq.ContactPerson) { existing.ContactPerson = supReq.ContactPerson; changed = true; }
                        if (!existing.IsActive) { existing.IsActive = true; changed = true; }
                        existing.SyncStatus = SyncStatus.Synced;

                        if (changed)
                        {
                            existing.UpdatedAt = DateTime.UtcNow;
                            updatedSuppliers++;
                        }
                    }
                    else
                    {
                        _db.Suppliers.Add(new CloudSupplier
                        {
                            Id = supReq.Id != Guid.Empty ? supReq.Id : Guid.NewGuid(),
                            TenantId = tenant.Id,
                            Name = supReq.Name,
                            Phone = supReq.Phone,
                            Address = supReq.Address,
                            Email = supReq.Email,
                            ContactPerson = supReq.ContactPerson,
                            Balance = supReq.Balance,
                            IsActive = true,
                            SyncStatus = SyncStatus.Synced,
                            CreatedAt = DateTime.UtcNow
                        });
                        updatedSuppliers++;
                    }
                }
            }

            // 2. Sync Products
            if (req.Products != null && req.Products.Any())
            {
                var existingProducts = await _db.Products.Where(p => p.TenantId == tenant.Id).ToDictionaryAsync(p => p.Id);

                foreach (var prodReq in req.Products)
                {
                    if (existingProducts.TryGetValue(prodReq.Id, out var existing))
                    {
                        bool changed = false;
                        if (existing.Barcode != prodReq.Barcode) { existing.Barcode = prodReq.Barcode; changed = true; }
                        if (existing.NameAr != prodReq.NameAr) { existing.NameAr = prodReq.NameAr; changed = true; }
                        if (existing.NameEn != prodReq.NameEn) { existing.NameEn = prodReq.NameEn; changed = true; }
                        if (existing.BaseUnit != prodReq.BaseUnit) { existing.BaseUnit = prodReq.BaseUnit; changed = true; }
                        if (existing.ParentUnit != prodReq.ParentUnit) { existing.ParentUnit = prodReq.ParentUnit; changed = true; }
                        if (existing.ConversionFactor != prodReq.ConversionFactor) { existing.ConversionFactor = prodReq.ConversionFactor; changed = true; }
                        if (existing.PurchasePrice != prodReq.PurchasePrice) { existing.PurchasePrice = prodReq.PurchasePrice; changed = true; }
                        if (existing.SellingPrice != prodReq.SellingPrice) { existing.SellingPrice = prodReq.SellingPrice; changed = true; }
                        if (existing.WholesalePrice != prodReq.WholesalePrice) { existing.WholesalePrice = prodReq.WholesalePrice; changed = true; }
                        if (existing.StockQuantity != prodReq.StockQuantity) { existing.StockQuantity = prodReq.StockQuantity; changed = true; }
                        if (existing.TrackExpiry != prodReq.TrackExpiry) { existing.TrackExpiry = prodReq.TrackExpiry; changed = true; }
                        if (existing.CategoryId != prodReq.CategoryId) { existing.CategoryId = prodReq.CategoryId; changed = true; }
                        if (existing.CategoryName != prodReq.CategoryName) { existing.CategoryName = prodReq.CategoryName; changed = true; }
                        if (existing.IsWeighable != prodReq.IsWeighable) { existing.IsWeighable = prodReq.IsWeighable; changed = true; }
                        if (existing.ShelfLifeDays != prodReq.ShelfLifeDays) { existing.ShelfLifeDays = prodReq.ShelfLifeDays; changed = true; }
                        if (existing.ExpiryAlertDays != prodReq.ExpiryAlertDays) { existing.ExpiryAlertDays = prodReq.ExpiryAlertDays; changed = true; }
                        if (existing.ReorderLevel != prodReq.ReorderLevel) { existing.ReorderLevel = prodReq.ReorderLevel; changed = true; }

                        if (changed)
                        {
                            existing.UpdatedAt = DateTime.UtcNow;
                            updatedProducts++;
                        }
                    }
                    else
                    {
                        _db.Products.Add(new CloudProduct
                        {
                            Id = prodReq.Id,
                            TenantId = tenant.Id,
                            Barcode = prodReq.Barcode,
                            NameAr = prodReq.NameAr,
                            NameEn = prodReq.NameEn,
                            BaseUnit = prodReq.BaseUnit,
                            ParentUnit = prodReq.ParentUnit,
                            ConversionFactor = prodReq.ConversionFactor,
                            PurchasePrice = prodReq.PurchasePrice,
                            SellingPrice = prodReq.SellingPrice,
                            WholesalePrice = prodReq.WholesalePrice,
                            StockQuantity = prodReq.StockQuantity,
                            CategoryId = prodReq.CategoryId,
                            CategoryName = prodReq.CategoryName,
                            IsWeighable = prodReq.IsWeighable,
                            ShelfLifeDays = prodReq.ShelfLifeDays,
                            ExpiryAlertDays = prodReq.ExpiryAlertDays,
                            ReorderLevel = prodReq.ReorderLevel,
                            TrackExpiry = prodReq.TrackExpiry,
                            IsActive = true,
                            SyncStatus = SyncStatus.Synced,
                            UpdatedAt = DateTime.UtcNow
                        });
                        updatedProducts++;
                    }
                }
            }

            // 3. Sync Categories
            int updatedCategories = 0;
            if (req.Categories != null && req.Categories.Any())
            {
                var existingCats = await _db.Categories.Where(c => c.TenantId == tenant.Id).ToDictionaryAsync(c => c.Id);
                foreach (var catReq in req.Categories)
                {
                    if (existingCats.TryGetValue(catReq.Id, out var existing))
                    {
                        if (existing.NameAr != catReq.NameAr || existing.NameEn != catReq.NameEn)
                        {
                            existing.NameAr = catReq.NameAr;
                            existing.NameEn = catReq.NameEn;
                            existing.UpdatedAt = DateTime.UtcNow;
                            updatedCategories++;
                        }
                    }
                    else
                    {
                        _db.Categories.Add(new CloudCategory
                        {
                            Id = catReq.Id,
                            TenantId = tenant.Id,
                            NameAr = catReq.NameAr,
                            NameEn = catReq.NameEn,
                            IsActive = true,
                            SyncStatus = SyncStatus.Synced,
                            CreatedAt = DateTime.UtcNow
                        });
                        updatedCategories++;
                    }
                }
            }

            if (updatedSuppliers > 0 || updatedProducts > 0 || updatedCategories > 0)
            {
                await _db.SaveChangesAsync();
            }

            return Ok(new SyncResultDto(
                Success: true,
                ProcessedCount: updatedSuppliers + updatedProducts + updatedCategories,
                Message: $"تم تحديث {updatedSuppliers} مورد و {updatedProducts} منتج و {updatedCategories} تصنيف بنجاح على السحابة."
            ));
        }

        // ==================== 6b. RETURNS PUSH FROM DESKTOP ====================

        [HttpPost("returns/push")]
        public async Task<IActionResult> PushReturns([FromBody] PushReturnsRequest req)
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            if (req.Returns == null || !req.Returns.Any())
                return Ok(new { success = true, count = 0 });

            var existingReturnNumbers = await _db.Returns
                .Where(r => r.TenantId == tenant.Id)
                .Select(r => r.ReturnNumber)
                .ToListAsync();

            var existingSet = new HashSet<string>(existingReturnNumbers, StringComparer.OrdinalIgnoreCase);
            int addedCount = 0;

            foreach (var ret in req.Returns)
            {
                if (existingSet.Contains(ret.ReturnNumber.Trim()))
                    continue;

                _db.Returns.Add(new CloudReturn
                {
                    Id = ret.Id != Guid.Empty ? ret.Id : Guid.NewGuid(),
                    TenantId = tenant.Id,
                    ReturnNumber = ret.ReturnNumber.Trim(),
                    Type = ret.Type ?? "Sales",
                    OriginalInvoiceNumber = ret.OriginalInvoiceNumber,
                    PartyName = ret.PartyName,
                    PartyPhone = ret.PartyPhone,
                    ReturnDate = ret.ReturnDate,
                    TotalAmount = ret.TotalAmount,
                    RefundMethod = ret.RefundMethod ?? "نقداً",
                    Reason = ret.Reason,
                    Notes = ret.Notes,
                    ItemsJson = ret.ItemsJson,
                    ItemsCount = ret.ItemsCount > 0 ? ret.ItemsCount : 1,
                    CreatedAt = DateTime.UtcNow
                });

                existingSet.Add(ret.ReturnNumber.Trim());
                addedCount++;
            }

            if (addedCount > 0)
            {
                await _db.SaveChangesAsync();
            }

            return Ok(new { success = true, addedCount });
        }

        // ==================== 7. EXPENSES SYNC ====================

        [HttpGet("expenses/pending")]
        public async Task<IActionResult> GetPendingExpenses()
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            var pending = await _db.Expenses
                .AsNoTracking()
                .Where(e => e.TenantId == tenant.Id && e.SyncStatus == SyncStatus.PendingSync)
                .OrderBy(e => e.CreatedAt)
                .Take(50)
                .ToListAsync();

            var result = pending.Select(e => new CloudExpenseSyncDto(
                e.Id,
                e.Title,
                e.Amount,
                e.Category,
                e.Date,
                e.Notes
            )).ToList();

            return Ok(result);
        }

        [HttpPost("expenses/{id:guid}/acknowledge")]
        public async Task<IActionResult> AcknowledgeExpense(Guid id)
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tenant.Id);
            if (expense == null) return NotFound();

            expense.SyncStatus = SyncStatus.Synced;
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [HttpPost("expenses/push")]
        public async Task<IActionResult> PushExpenses([FromBody] PushExpensesRequest req)
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            if (req.Expenses == null || !req.Expenses.Any())
                return Ok(new { count = 0 });

            int added = 0;
            var expenseIds = req.Expenses.Select(e => e.Id).ToList();
            var existingIds = await _db.Expenses
                .Where(e => e.TenantId == tenant.Id && expenseIds.Contains(e.Id))
                .Select(e => e.Id)
                .ToListAsync();

            foreach (var item in req.Expenses)
            {
                if (!existingIds.Contains(item.Id))
                {
                    _db.Expenses.Add(new CloudExpense
                    {
                        Id = item.Id,
                        TenantId = tenant.Id,
                        Title = item.Title,
                        Amount = item.Amount,
                        Category = item.Category,
                        Date = item.Date,
                        Notes = item.Notes,
                        SyncStatus = SyncStatus.Synced,
                        CreatedAt = DateTime.UtcNow
                    });
                    added++;
                }
            }

            if (added > 0)
            {
                await _db.SaveChangesAsync();
            }

            return Ok(new { success = true, addedCount = added });
        }

        // ==================== 8. EXPIRY NOTIFICATIONS SYNC ====================

        [HttpPost("notifications/push")]
        public async Task<IActionResult> PushExpiryNotifications([FromBody] PushExpiryNotificationsRequest req)
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            // Retain existing records that have pending mobile actions
            var pendingActionIds = await _db.ExpiryNotifications
                .Where(n => n.TenantId == tenant.Id && n.SyncStatus == SyncStatus.PendingSync)
                .Select(n => n.Id)
                .ToListAsync();

            var toRemove = await _db.ExpiryNotifications
                .Where(n => n.TenantId == tenant.Id && !pendingActionIds.Contains(n.Id))
                .ToListAsync();

            _db.ExpiryNotifications.RemoveRange(toRemove);

            if (req.Notifications != null && req.Notifications.Any())
            {
                foreach (var item in req.Notifications)
                {
                    if (!pendingActionIds.Contains(item.Id))
                    {
                        _db.ExpiryNotifications.Add(new CloudExpiryNotification
                        {
                            Id = item.Id,
                            TenantId = tenant.Id,
                            ProductId = item.ProductId,
                            ProductName = item.ProductName,
                            Barcode = item.Barcode,
                            BatchId = item.BatchId,
                            BatchNumber = item.BatchNumber,
                            RemainingQuantity = item.RemainingQuantity,
                            Unit = item.Unit,
                            UnitCost = item.UnitCost,
                            ExpiryDate = item.ExpiryDate,
                            DaysRemaining = item.DaysRemaining,
                            IsExpired = item.IsExpired,
                            Message = item.Message,
                            Status = item.Status,
                            SyncStatus = SyncStatus.Synced,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true, count = req.Notifications?.Count ?? 0 });
        }

        [HttpGet("notifications/pending-actions")]
        public async Task<IActionResult> GetPendingNotificationActions()
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            var pending = await _db.ExpiryNotifications
                .AsNoTracking()
                .Where(n => n.TenantId == tenant.Id && n.SyncStatus == SyncStatus.PendingSync)
                .ToListAsync();

            var result = pending.Select(n => new PendingNotificationActionDto(
                n.Id,
                n.ProductId,
                n.BatchId,
                n.ActionType ?? "OK",
                n.ActionQuantity,
                n.ActionReason,
                n.NewExpiryDate,
                n.NewBatchNumber,
                n.ActionNotes,
                n.ActionTakenAt ?? DateTime.UtcNow
            )).ToList();

            return Ok(result);
        }

        [HttpPost("notifications/actions/{id:guid}/acknowledge")]
        public async Task<IActionResult> AcknowledgeNotificationAction(Guid id)
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            var notif = await _db.ExpiryNotifications.FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenant.Id);
            if (notif == null) return NotFound();

            notif.SyncStatus = SyncStatus.Synced;
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }

        // ==================== 9. SHIFT SUMMARY PUSH (CASHIER PERFORMANCE) ====================

        [HttpPost("shifts/closed-push")]
        public async Task<IActionResult> PushClosedShift([FromBody] PushClosedShiftRequest req)
        {
            var tenant = await AuthenticateSyncClientAsync();
            if (tenant == null) return Unauthorized();

            var existing = await _db.ShiftSummaries.FirstOrDefaultAsync(s => s.TenantId == tenant.Id && s.ShiftId == req.ShiftId);
            if (existing == null)
            {
                var summary = new CloudShiftSummary
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    ShiftId = req.ShiftId,
                    CashierName = req.CashierName,
                    OpenedAt = req.OpenedAt,
                    ClosedAt = req.ClosedAt,
                    OpeningCash = req.OpeningCash,
                    ActualClosingCash = req.ActualClosingCash,
                    SystemCash = req.SystemCash,
                    CashDifference = req.CashDifference,
                    TotalSales = req.TotalSales,
                    TotalCash = req.TotalCash,
                    TotalCard = req.TotalCard,
                    TotalWallet = req.TotalWallet,
                    TotalCredit = req.TotalCredit,
                    TotalInvoices = req.TotalInvoices,
                    TotalReturns = req.TotalReturns,
                    ClosingNotes = req.ClosingNotes,
                    IsReadByOwner = false,
                    CreatedAt = DateTime.UtcNow
                };

                _db.ShiftSummaries.Add(summary);
                await _db.SaveChangesAsync();
            }

            return Ok(new { success = true });
        }
    }
}
