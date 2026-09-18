using Inventory.Application.Catalog.Products.Commands.CreateProduct;
using Inventory.Infrastructre.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using POS.Shared.Domain;
using Purchases.Application.Purchases.Commands.CreatePurchase;
using Purchases.Application.Suppliers.Commands.CreateSupplier;
using Purchases.Infrastructre.Database;
using Settings.Application.StoreSettings.Queries.GetSettings;
using Expenses.Infrastructre.Database;
using Expenses.Domain.Expenses.Entities;
using Returns.Infrastructre.Database;
using Sales.Infrastructre.Database;
using System.Net.Http.Json;
using System.Text.Json;

namespace POS.WebAPI.Services
{
    public class CloudSyncBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CloudSyncBackgroundService> _logger;
        private readonly HttpClient _cloudHttp;
        private readonly string _syncApiKey = "KEY-SHOP01-SECURE-SYNC-2026";
        private readonly string _shopCode = "SHOP01";
        private DateTime _lastCatalogPush = DateTime.MinValue;
        private DateTime _lastDashboardPush = DateTime.MinValue;
        private readonly HashSet<string> _knownInvoiceNumbers = new(StringComparer.OrdinalIgnoreCase);

        public CloudSyncBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<CloudSyncBackgroundService> logger,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;

            var cloudUrl = configuration["CloudSync:Url"] ?? "http://localhost:5100/";
            _syncApiKey = configuration["CloudSync:ApiKey"] ?? _syncApiKey;
            _shopCode = configuration["CloudSync:ShopCode"] ?? _shopCode;

            _cloudHttp = new HttpClient
            {
                BaseAddress = new Uri(cloudUrl.EndsWith('/') ? cloudUrl : $"{cloudUrl}/"),
                Timeout = TimeSpan.FromSeconds(25)
            };
            _cloudHttp.DefaultRequestHeaders.Add("X-Sync-ApiKey", _syncApiKey);
            _cloudHttp.DefaultRequestHeaders.Add("X-Shop-Code", _shopCode);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[CloudSyncBackgroundService] Started. Periodic sync interval: 15 seconds.");

            // Wait 3 seconds on startup before initial sync run
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformSyncCycleAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[CloudSyncBackgroundService] Error during sync cycle: {Message}", ex.Message);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task PerformSyncCycleAsync(CancellationToken ct)
        {
            // 1. Health check to Cloud API
            bool isOnline = await CheckCloudOnlineAsync(ct);
            if (!isOnline)
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var purchasesDb = scope.ServiceProvider.GetRequiredService<PurchasesDbContext>();
            var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var expensesDb = scope.ServiceProvider.GetRequiredService<ExpensesDbContext>();

            // 1.5 PRIORITY #0: Pull Pending Categories from Cloud
            await PullPendingCategoriesFromCloudAsync(mediator, inventoryDb, ct);

            // 2. PRIORITY #1: Pull Pending Suppliers from Cloud (so purchases find them)
            await PullPendingSuppliersFromCloudAsync(mediator, purchasesDb, ct);

            // 3. PRIORITY #2: Pull Pending Purchases from Cloud
            int purchasesCount = await PullPendingPurchasesFromCloudAsync(mediator, purchasesDb, inventoryDb, ct);

            // 4. PRIORITY #3: Pull Pending Products from Cloud
            await PullPendingProductsFromCloudAsync(mediator, inventoryDb, ct);

            // 4b. Pull Pending Expenses from Cloud
            await PullPendingExpensesFromCloudAsync(expensesDb, ct);

            // 4c. Pull Pending Notification Actions from Cloud (OK, Waste, SupplierReplacement)
            await PullPendingNotificationActionsFromCloudAsync(mediator, inventoryDb, ct);

            // 4d. Push Local Expenses to Cloud
            await PushLocalExpensesToCloudAsync(expensesDb, ct);

            // 4e. Pull Pending Debt Payments from Mobile
            int debtsCount = await PullPendingDebtPaymentsFromCloudAsync(mediator, scope.ServiceProvider, ct);

            // 4f. Push Local Returns to Cloud
            await PushReturnsToCloudAsync(scope.ServiceProvider, ct);

            // 5. Push Store Settings to Cloud
            await PushStoreSettingsToCloudAsync(mediator, ct);

            // 6. Push Dashboard snapshot, debts, and active notifications
            if (purchasesCount > 0 || debtsCount > 0 || (DateTime.UtcNow - _lastDashboardPush) > TimeSpan.FromSeconds(60))
            {
                await PushDebtsAndDashboardAsync(scope.ServiceProvider, ct);
                await PushActiveExpiryNotificationsAsync(inventoryDb, ct);
                _lastDashboardPush = DateTime.UtcNow;
            }

            // 7. Push Catalog periodically
            if ((DateTime.UtcNow - _lastCatalogPush) > TimeSpan.FromMinutes(10))
            {
                await PushCatalogToCloudAsync(inventoryDb, purchasesDb, ct);
                _lastCatalogPush = DateTime.UtcNow;
            }
        }

        private async Task<bool> CheckCloudOnlineAsync(CancellationToken ct)
        {
            try
            {
                var response = await _cloudHttp.GetAsync("api/sync/health", ct);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task PullPendingSuppliersFromCloudAsync(IMediator mediator, PurchasesDbContext purchasesDb, CancellationToken ct)
        {
            try
            {
                var pendingSuppliers = await _cloudHttp.GetFromJsonAsync<List<PendingSupplierSyncDto>>("api/sync/suppliers/pending", ct);
                if (pendingSuppliers == null || !pendingSuppliers.Any()) return;

                var existingSuppliers = await purchasesDb.Suppliers.AsNoTracking().ToListAsync(ct);
                var existingById = existingSuppliers.ToDictionary(s => s.Id);
                var existingByName = existingSuppliers.ToDictionary(s => s.Name.Trim().ToLowerInvariant(), s => s);

                foreach (var sup in pendingSuppliers)
                {
                    if (existingById.ContainsKey(sup.Id) || existingByName.ContainsKey(sup.Name.Trim().ToLowerInvariant()))
                    {
                        await _cloudHttp.PostAsync($"api/sync/suppliers/{sup.Id}/acknowledge", null, ct);
                        continue;
                    }

                    var cmd = new CreateSupplierCommand(
                        sup.Name.Trim(),
                        !string.IsNullOrWhiteSpace(sup.Phone) ? sup.Phone.Trim() : "01000000000",
                        sup.Email?.Trim(),
                        sup.Address?.Trim(),
                        sup.ContactPerson?.Trim(),
                        sup.Id
                    );

                    var res = await mediator.Send(cmd, ct);
                    if (res.IsSuccess)
                    {
                        await _cloudHttp.PostAsync($"api/sync/suppliers/{sup.Id}/acknowledge", null, ct);
                        _logger.LogInformation("[CloudSync] Imported supplier: {Name} ({Id})", sup.Name, sup.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[CloudSync] Error pulling suppliers: {Message}", ex.Message);
            }
        }

        private async Task<int> PullPendingPurchasesFromCloudAsync(IMediator mediator, PurchasesDbContext purchasesDb, InventoryDbContext inventoryDb, CancellationToken ct)
        {
            int imported = 0;
            try
            {
                var pendingPurchases = await _cloudHttp.GetFromJsonAsync<List<PendingPurchaseSyncDto>>("api/sync/purchases/pending", ct);
                if (pendingPurchases == null || !pendingPurchases.Any()) return 0;

                if (!_knownInvoiceNumbers.Any())
                {
                    var existingNumbers = await purchasesDb.Purchases.AsNoTracking().Select(p => p.InvoiceNumber).ToListAsync(ct);
                    foreach (var num in existingNumbers)
                    {
                        if (!string.IsNullOrWhiteSpace(num)) _knownInvoiceNumbers.Add(num.Trim());
                    }
                }

                // Use the first user in the system or standard fallback admin user ID
                var adminUserId = Guid.Parse("2bc4e49b-fe29-4c7b-9eed-649de1c32cef");

                var affectedProductIds = new HashSet<Guid>();
                var affectedSupplierIds = new HashSet<Guid>();

                var localProducts = await inventoryDb.Products.AsNoTracking().ToListAsync(ct);
                var localById = localProducts.ToDictionary(p => p.Id);
                var localByBarcode = localProducts
                    .Where(p => !string.IsNullOrWhiteSpace(p.Barcode))
                    .GroupBy(p => p.Barcode.Trim().ToLower())
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var cloudPurchase in pendingPurchases)
                {
                    if (_knownInvoiceNumbers.Contains(cloudPurchase.InvoiceNumber.Trim()))
                    {
                        await _cloudHttp.PostAsync($"api/sync/purchases/{cloudPurchase.Id}/acknowledge", null, ct);
                        continue;
                    }

                    var items = cloudPurchase.Items.Select(i =>
                    {
                        Guid resolvedProductId = i.ProductId;
                        if (!localById.ContainsKey(resolvedProductId))
                        {
                            if (!string.IsNullOrWhiteSpace(i.Barcode) && localByBarcode.TryGetValue(i.Barcode.Trim().ToLower(), out var pMatch))
                            {
                                resolvedProductId = pMatch.Id;
                            }
                        }

                        return new CreatePurchaseItemRequest(
                            resolvedProductId,
                            i.Quantity,
                            i.UnitCost,
                            i.Discount,
                            i.Tax,
                            i.ExpiryDate,
                            i.BatchNumber,
                            i.Unit
                        );
                    }).ToList();

                    var cmd = new CreatePurchaseCommand(
                        cloudPurchase.InvoiceNumber,
                        cloudPurchase.SupplierId,
                        adminUserId,
                        items,
                        cloudPurchase.InternalNumber,
                        cloudPurchase.DiscountAmount,
                        cloudPurchase.TaxAmount,
                        cloudPurchase.PaidAmount,
                        cloudPurchase.PaymentMethod,
                        $"[وارد من تطبيق الموبايل] {cloudPurchase.Notes}".Trim(),
                        cloudPurchase.PurchaseDate
                    );

                    var res = await mediator.Send(cmd, ct);
                    if (res.IsSuccess)
                    {
                        await _cloudHttp.PostAsync($"api/sync/purchases/{cloudPurchase.Id}/acknowledge", null, ct);
                        _knownInvoiceNumbers.Add(cloudPurchase.InvoiceNumber.Trim());
                        imported++;

                        foreach (var item in items)
                        {
                            affectedProductIds.Add(item.ProductId);
                        }
                        if (cloudPurchase.SupplierId != Guid.Empty)
                        {
                            affectedSupplierIds.Add(cloudPurchase.SupplierId);
                        }

                        _logger.LogInformation("[CloudSync] Successfully imported purchase: {InvoiceNumber}", cloudPurchase.InvoiceNumber);
                    }
                    else
                    {
                        var errorMsg = res.Error?.Name ?? res.Error?.Code ?? "Failed to create local purchase.";
                        _logger.LogWarning("[CloudSync] Failed to import purchase {InvoiceNumber}: {Error}", cloudPurchase.InvoiceNumber, errorMsg);
                        await _cloudHttp.PostAsJsonAsync($"api/sync/purchases/{cloudPurchase.Id}/fail", new { reason = errorMsg }, ct);
                    }
                }

                // Fast stock & supplier balances push
                if (imported > 0)
                {
                    await PushFastStockAndSuppliersAsync(inventoryDb, purchasesDb, affectedProductIds, affectedSupplierIds, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[CloudSync] Error pulling purchases: {Message}", ex.Message);
            }
            return imported;
        }

        private async Task PushFastStockAndSuppliersAsync(InventoryDbContext inventoryDb, PurchasesDbContext purchasesDb, IEnumerable<Guid> productIds, IEnumerable<Guid> supplierIds, CancellationToken ct)
        {
            try
            {
                if (productIds.Any())
                {
                    var stockUpdates = await inventoryDb.Products
                        .Where(p => productIds.Contains(p.Id))
                        .Select(p => new StockUpdateSyncItem(p.Id, p.QuantityInStock))
                        .ToListAsync(ct);

                    if (stockUpdates.Any())
                    {
                        await _cloudHttp.PostAsJsonAsync("api/sync/stock/push", new { items = stockUpdates }, ct);
                    }
                }

                if (supplierIds.Any())
                {
                    var supDebts = await purchasesDb.Purchases
                        .Where(p => supplierIds.Contains(p.SupplierId))
                        .GroupBy(p => p.SupplierId)
                        .Select(g => new SupplierBalanceSyncItem(g.Key, g.Sum(p => p.RemainingAmount)))
                        .ToListAsync(ct);

                    if (supDebts.Any())
                    {
                        await _cloudHttp.PostAsJsonAsync("api/sync/suppliers/balances", new { items = supDebts }, ct);
                    }
                }
            }
            catch { }
        }

        private async Task PullPendingProductsFromCloudAsync(IMediator mediator, InventoryDbContext inventoryDb, CancellationToken ct)
        {
            try
            {
                var pendingProducts = await _cloudHttp.GetFromJsonAsync<List<PendingProductSyncDto>>("api/sync/products/pending", ct);
                if (pendingProducts == null || !pendingProducts.Any()) return;

                var defaultCatId = await inventoryDb.Categories.Select(c => c.Id).FirstOrDefaultAsync(ct);
                var defaultUnitId = await inventoryDb.Units.Select(u => u.Id).FirstOrDefaultAsync(ct);

                var localProducts = await inventoryDb.Products.AsNoTracking().ToListAsync(ct);
                var localById = localProducts.ToDictionary(p => p.Id);
                var localByBarcode = localProducts
                    .Where(p => !string.IsNullOrWhiteSpace(p.Barcode))
                    .GroupBy(p => p.Barcode.Trim().ToLower())
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var prod in pendingProducts)
                {
                    var barcodeClean = prod.Barcode.Trim().ToLower();
                    if (localById.ContainsKey(prod.Id) || localByBarcode.ContainsKey(barcodeClean))
                    {
                        await _cloudHttp.PostAsync($"api/sync/products/{prod.Id}/acknowledge", null, ct);
                        continue;
                    }

                    var cmd = new CreateProductCommand(
                        prod.Barcode,
                        prod.NameAr,
                        prod.NameEn ?? string.Empty,
                        prod.CategoryId ?? defaultCatId,
                        defaultUnitId,
                        prod.PurchasePrice,
                        prod.SellingPrice,
                        prod.WholesalePrice,
                        null,
                        null,
                        prod.BaseUnit,
                        prod.ParentUnit,
                        prod.ConversionFactor,
                        prod.ShelfLifeDays,
                        prod.ExpiryAlertDays,
                        prod.ReorderLevel,
                        100,
                        prod.IsWeighable,
                        true,
                        prod.TrackExpiry,
                        0,
                        null,
                        prod.Id,
                        prod.StockQuantity
                    );

                    var res = await mediator.Send(cmd, ct);
                    if (res.IsSuccess || (res.Error != null && (res.Error.Name.Contains("الباركود مسجل مسبقاً") || res.Error.Code.Contains("Duplicate"))))
                    {
                        await _cloudHttp.PostAsync($"api/sync/products/{prod.Id}/acknowledge", null, ct);
                        _logger.LogInformation("[CloudSync] Acknowledged product: {NameAr}", prod.NameAr);
                    }
                }
            }
            catch { }
        }

        private async Task PushStoreSettingsToCloudAsync(IMediator mediator, CancellationToken ct)
        {
            try
            {
                var settingsResult = await mediator.Send(new GetSettingsQuery(), ct);
                if (!settingsResult.IsSuccess || settingsResult.Value == null) return;

                var s = settingsResult.Value;
                var req = new
                {
                    storeName = s.StoreName,
                    address = s.Address,
                    phone = s.Phone,
                    taxRate = s.TaxRate,
                    isTaxIncluded = s.IsTaxIncluded,
                    currency = string.IsNullOrWhiteSpace(s.Currency) ? "ج.م" : s.Currency,
                    invoiceFooterMessage = s.InvoiceFooterMessage,
                    allowNegativeStock = s.AllowNegativeStock,
                    logoUrl = s.LogoUrl
                };

                await _cloudHttp.PostAsJsonAsync("api/sync/store-settings/push", req, ct);
            }
            catch { }
        }

        private async Task PushDebtsAndDashboardAsync(IServiceProvider sp, CancellationToken ct)
        {
            try
            {
                var purchasesDb = sp.GetRequiredService<PurchasesDbContext>();
                var salesDb = sp.GetService<Sales.Infrastructre.Database.SalesDbContext>();
                var inventoryDb = sp.GetRequiredService<InventoryDbContext>();
                var expensesDb = sp.GetService<ExpensesDbContext>();

                // 1. Debts with real Customer & Supplier names
                var debtsList = new List<object>();

                if (salesDb != null)
                {
                    var customerDebts = await (
                        from s in salesDb.Sales
                        where s.PaidAmount < s.TotalAmount
                        join c in salesDb.Customers on s.CustomerId equals (Guid?)c.Id into cGroup
                        from cust in cGroup.DefaultIfEmpty()
                        select new
                        {
                            id = Guid.NewGuid(),
                            type = "Customer",
                            referenceId = s.Id,
                            invoiceNumber = s.InvoiceNumber,
                            entityName = cust != null && !string.IsNullOrWhiteSpace(cust.Name) ? cust.Name : "عميل نقدي",
                            phone = cust != null ? cust.Phone : (string?)null,
                            totalAmount = s.TotalAmount,
                            paidAmount = s.PaidAmount,
                            remainingAmount = s.TotalAmount - s.PaidAmount,
                            date = s.SaleDate
                        }
                    )
                    .Take(100)
                    .ToListAsync(ct);

                    debtsList.AddRange(customerDebts);
                }

                var supplierDebts = await (
                    from p in purchasesDb.Purchases
                    where p.RemainingAmount > 0
                    join sup in purchasesDb.Suppliers on p.SupplierId equals (Guid?)sup.Id into sGroup
                    from s in sGroup.DefaultIfEmpty()
                    select new
                    {
                        id = Guid.NewGuid(),
                        type = "Supplier",
                        referenceId = p.Id,
                        invoiceNumber = p.InvoiceNumber,
                        entityName = s != null && !string.IsNullOrWhiteSpace(s.Name) ? s.Name : "مورد",
                        phone = s != null ? s.Phone : (string?)null,
                        totalAmount = p.TotalAmount,
                        paidAmount = p.PaidAmount,
                        remainingAmount = p.RemainingAmount,
                        date = p.PurchaseDate
                    }
                )
                .Take(100)
                .ToListAsync(ct);

                debtsList.AddRange(supplierDebts);

                if (debtsList.Any())
                {
                    await _cloudHttp.PostAsJsonAsync("api/sync/debts/push", new { debts = debtsList }, ct);
                }

                // 2. Dashboard metrics
                var today = DateTime.UtcNow.Date;
                var monthStart = new DateTime(today.Year, today.Month, 1);

                decimal todayPurchases = await purchasesDb.Purchases
                    .Where(p => p.PurchaseDate >= today)
                    .SumAsync(p => (decimal?)p.TotalAmount, ct) ?? 0;

                decimal monthPurchases = await purchasesDb.Purchases
                    .Where(p => p.PurchaseDate >= monthStart)
                    .SumAsync(p => (decimal?)p.TotalAmount, ct) ?? 0;

                decimal supDebtsTotal = await purchasesDb.Purchases
                    .SumAsync(p => (decimal?)p.RemainingAmount, ct) ?? 0;

                decimal todaySales = 0;
                decimal monthSales = 0;
                decimal customerDebtsTotal = 0;

                if (salesDb != null)
                {
                    todaySales = await salesDb.Sales
                        .Where(s => s.SaleDate >= today)
                        .SumAsync(s => (decimal?)s.TotalAmount, ct) ?? 0;

                    monthSales = await salesDb.Sales
                        .Where(s => s.SaleDate >= monthStart)
                        .SumAsync(s => (decimal?)s.TotalAmount, ct) ?? 0;

                    customerDebtsTotal = await salesDb.Sales
                        .Where(s => s.PaidAmount < s.TotalAmount)
                        .SumAsync(s => (decimal?)(s.TotalAmount - s.PaidAmount), ct) ?? 0;
                }

                decimal todayExpenses = 0;
                decimal monthExpenses = 0;
                if (expensesDb != null)
                {
                    todayExpenses = await expensesDb.Expenses
                        .Where(e => e.ExpenseDate >= today)
                        .SumAsync(e => (decimal?)e.Amount, ct) ?? 0;

                    monthExpenses = await expensesDb.Expenses
                        .Where(e => e.ExpenseDate >= monthStart)
                        .SumAsync(e => (decimal?)e.Amount, ct) ?? 0;
                }

                decimal todayWasteLoss = await inventoryDb.Wastes
                    .Where(w => w.CreatedAt >= today)
                    .SumAsync(w => (decimal?)w.TotalCost, ct) ?? 0;

                decimal monthWasteLoss = await inventoryDb.Wastes
                    .Where(w => w.CreatedAt >= monthStart)
                    .SumAsync(w => (decimal?)w.TotalCost, ct) ?? 0;

                decimal totalWasteLoss = await inventoryDb.Wastes
                    .SumAsync(w => (decimal?)w.TotalCost, ct) ?? 0;

                int lowStockCount = await inventoryDb.Products
                    .CountAsync(p => p.QuantityInStock <= p.ReorderLevel && p.IsActive, ct);

                int expiryAlertsCount = await inventoryDb.Notifications
                    .CountAsync(n => n.Status == "Active", ct);

                var dashReq = new
                {
                    todaySales,
                    todayProfit = todaySales - (todayPurchases * 0.7m) - todayExpenses,
                    todayPurchases,
                    todayExpenses,
                    monthSales,
                    monthProfit = monthSales - (monthPurchases * 0.7m) - monthExpenses,
                    monthPurchases,
                    monthExpenses,
                    customerDebtsTotal,
                    supplierDebtsTotal = supDebtsTotal,
                    lowStockCount,
                    expiryAlertsCount,
                    monthlySalesJson = (string?)null,
                    todayWasteLoss,
                    monthWasteLoss,
                    totalWasteLoss
                };

                await _cloudHttp.PostAsJsonAsync("api/sync/dashboard/push", dashReq, ct);
            }
            catch { }
        }

        private async Task PushCatalogToCloudAsync(InventoryDbContext inventoryDb, PurchasesDbContext purchasesDb, CancellationToken ct)
        {
            try
            {
                var products = await inventoryDb.Products.AsNoTracking().Where(p => p.IsActive).ToListAsync(ct);
                var suppliers = await purchasesDb.Suppliers.AsNoTracking().ToListAsync(ct);
                var categories = await inventoryDb.Categories.AsNoTracking().ToListAsync(ct);

                var catDtos = categories.Select(c => new
                {
                    id = c.Id,
                    nameAr = c.NameAr,
                    nameEn = c.NameEn
                }).ToList();

                var prodDtos = products.Select(p => new
                {
                    id = p.Id,
                    barcode = p.Barcode,
                    nameAr = p.NameAr,
                    nameEn = p.NameEn,
                    baseUnit = p.BaseUnit,
                    parentUnit = p.ParentUnit,
                    conversionFactor = p.ConversionFactor,
                    purchasePrice = p.PurchasePrice,
                    sellingPrice = p.SellingPrice,
                    wholesalePrice = p.WholesalePrice,
                    stockQuantity = p.QuantityInStock,
                    categoryId = p.CategoryId,
                    categoryName = categories.FirstOrDefault(c => c.Id == p.CategoryId)?.NameAr ?? "عام",
                    isWeighable = p.IsWeighable,
                    shelfLifeDays = p.ShelfLifeDays,
                    expiryAlertDays = p.ExpiryAlertDays,
                    reorderLevel = p.ReorderLevel,
                    trackExpiry = p.TrackExpiry
                }).ToList();

                var supDtos = suppliers.Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    phone = s.Phone,
                    address = s.Address,
                    balance = 0m,
                    email = s.Email,
                    contactPerson = s.ContactPerson
                }).ToList();

                var catalogReq = new
                {
                    products = prodDtos,
                    suppliers = supDtos,
                    categories = catDtos
                };

                await _cloudHttp.PostAsJsonAsync("api/sync/catalog/push", catalogReq, ct);
                _logger.LogInformation("[CloudSync] Pushed full catalog ({Products} products, {Suppliers} suppliers, {Categories} categories)",
                    prodDtos.Count, supDtos.Count, catDtos.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[CloudSync] Error pushing catalog: {Message}", ex.Message);
            }
        }

        private async Task PullPendingExpensesFromCloudAsync(ExpensesDbContext expensesDb, CancellationToken ct)
        {
            try
            {
                var response = await _cloudHttp.GetAsync("api/sync/expenses/pending", ct);
                if (!response.IsSuccessStatusCode) return;

                var items = await response.Content.ReadFromJsonAsync<List<PendingExpenseSyncDto>>(cancellationToken: ct);
                if (items == null || !items.Any()) return;

                foreach (var exp in items)
                {
                    var exists = await expensesDb.Expenses.AnyAsync(e => e.Id == exp.Id, ct);
                    if (!exists)
                    {
                        var expenseResult = Expense.Create(
                            exp.Title,
                            exp.Amount,
                            Guid.NewGuid(),
                            exp.Category,
                            exp.Date,
                            exp.Notes);

                        if (expenseResult.IsSuccess)
                        {
                            await expensesDb.Expenses.AddAsync(expenseResult.Value!, ct);
                            await expensesDb.SaveChangesAsync(ct);
                        }
                    }

                    await _cloudHttp.PostAsync($"api/sync/expenses/{exp.Id}/acknowledge", null, ct);
                }
            }
            catch { }
        }

        private async Task PushLocalExpensesToCloudAsync(ExpensesDbContext expensesDb, CancellationToken ct)
        {
            try
            {
                var recentExpenses = await expensesDb.Expenses
                    .AsNoTracking()
                    .OrderByDescending(e => e.CreatedAt)
                    .Take(50)
                    .Select(e => new
                    {
                        id = e.Id,
                        title = e.Title,
                        amount = e.Amount,
                        category = e.Description,
                        date = e.ExpenseDate,
                        notes = e.Notes
                    })
                    .ToListAsync(ct);

                if (recentExpenses.Any())
                {
                    await _cloudHttp.PostAsJsonAsync("api/sync/expenses/push", new { expenses = recentExpenses }, ct);
                }
            }
            catch { }
        }

        private async Task PullPendingNotificationActionsFromCloudAsync(IMediator mediator, InventoryDbContext inventoryDb, CancellationToken ct)
        {
            try
            {
                var res = await _cloudHttp.GetAsync("api/sync/notifications/pending-actions", ct);
                if (!res.IsSuccessStatusCode) return;

                var actions = await res.Content.ReadFromJsonAsync<List<PendingCloudActionDto>>(cancellationToken: ct);
                if (actions == null || !actions.Any()) return;

                foreach (var act in actions)
                {
                    bool handled = false;
                    if (act.ActionType == "OK")
                    {
                        var notif = await inventoryDb.Notifications.FirstOrDefaultAsync(n => n.Id == act.NotificationId, ct);
                        if (notif != null)
                        {
                            notif.Resolve(null);
                            await inventoryDb.SaveChangesAsync(ct);
                        }
                        handled = true;
                    }
                    else if (act.ActionType == "Waste" && act.BatchId.HasValue)
                    {
                        var cmd = new Inventory.Application.Stock.Waste.Commands.RecordWaste.RecordWasteCommand(
                            act.ProductId,
                            act.BatchId.Value,
                            act.Quantity,
                            "piece",
                            act.Reason ?? "انتهاء صلاحية",
                            Guid.NewGuid(),
                            act.Notes,
                            "Mobile",
                            act.NotificationId);

                        var r = await mediator.Send(cmd, ct);
                        handled = r.IsSuccess;
                    }
                    else if (act.ActionType == "SupplierReplacement" && act.BatchId.HasValue && act.NewExpiryDate.HasValue)
                    {
                        var cmd = new Inventory.Application.Batches.ProductBatches.Commands.ReplaceSupplierBatch.ReplaceSupplierBatchCommand(
                            act.BatchId.Value,
                            act.NewExpiryDate.Value,
                            act.NewBatchNumber,
                            act.Notes,
                            act.NotificationId);

                        var r = await mediator.Send(cmd, ct);
                        handled = r.IsSuccess;
                    }

                    if (handled)
                    {
                        await _cloudHttp.PostAsync($"api/sync/notifications/actions/{act.NotificationId}/acknowledge", null, ct);
                    }
                }
            }
            catch { }
        }

        private async Task PushActiveExpiryNotificationsAsync(InventoryDbContext inventoryDb, CancellationToken ct)
        {
            try
            {
                var activeNotifs = await inventoryDb.Notifications
                    .AsNoTracking()
                    .Where(n => n.Status == "Active")
                    .ToListAsync(ct);

                var list = new List<object>();
                foreach (var n in activeNotifs)
                {
                    var prod = await inventoryDb.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == n.ProductId, ct);
                    var batch = await inventoryDb.Batches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == n.BatchId, ct);

                    if (batch != null && batch.RemainingQuantity > 0)
                    {
                        list.Add(new
                        {
                            id = n.Id,
                            productId = n.ProductId,
                            productName = prod?.NameAr ?? "منتج",
                            barcode = prod?.Barcode,
                            batchId = (Guid?)batch.Id,
                            batchNumber = batch.BatchNumber,
                            remainingQuantity = batch.RemainingQuantity,
                            unit = prod?.BaseUnit ?? "قطعة",
                            unitCost = batch.UnitCost,
                            expiryDate = batch.ExpiryDate,
                            daysRemaining = batch.ExpiryDate.HasValue ? (int)(batch.ExpiryDate.Value.Date - DateTime.UtcNow.Date).TotalDays : 0,
                            isExpired = batch.ExpiryDate.HasValue && batch.ExpiryDate.Value.Date <= DateTime.UtcNow.Date,
                            message = n.Message,
                            status = n.Status
                        });
                    }
                }

                await _cloudHttp.PostAsJsonAsync("api/sync/notifications/push", new { notifications = list }, ct);
            }
            catch { }
        }

        private async Task PullPendingCategoriesFromCloudAsync(IMediator mediator, InventoryDbContext inventoryDb, CancellationToken ct)
        {
            try
            {
                var pendingCategories = await _cloudHttp.GetFromJsonAsync<List<PendingCategorySyncDto>>("api/sync/categories/pending", ct);
                if (pendingCategories == null || !pendingCategories.Any()) return;

                var existingCategories = await inventoryDb.Categories.AsNoTracking().ToListAsync(ct);
                var existingById = existingCategories.ToDictionary(c => c.Id);
                var existingByName = existingCategories.ToDictionary(c => c.NameAr.Trim().ToLowerInvariant(), c => c);

                foreach (var cat in pendingCategories)
                {
                    if (existingById.ContainsKey(cat.Id) || existingByName.ContainsKey(cat.NameAr.Trim().ToLowerInvariant()))
                    {
                        await _cloudHttp.PostAsync($"api/sync/categories/{cat.Id}/acknowledge", null, ct);
                        continue;
                    }

                    var cmd = new Inventory.Application.Catalog.Categories.Commands.CreateCategory.CreateCategoryCommand(
                        cat.NameAr.Trim(),
                        !string.IsNullOrWhiteSpace(cat.NameEn) ? cat.NameEn.Trim() : cat.NameAr.Trim(),
                        null,
                        cat.Id
                    );

                    var res = await mediator.Send(cmd, ct);
                    if (res.IsSuccess)
                    {
                        await _cloudHttp.PostAsync($"api/sync/categories/{cat.Id}/acknowledge", null, ct);
                        _logger.LogInformation("[CloudSync] Imported category from Cloud: {Name} ({Id})", cat.NameAr, cat.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[CloudSync] Error pulling categories: {Message}", ex.Message);
            }
        }

        private async Task<int> PullPendingDebtPaymentsFromCloudAsync(IMediator mediator, IServiceProvider sp, CancellationToken ct)
        {
            int count = 0;
            try
            {
                var pendingPayments = await _cloudHttp.GetFromJsonAsync<List<PendingDebtPaymentSyncDto>>("api/sync/debts/pending", ct);
                if (pendingPayments == null || !pendingPayments.Any()) return 0;

                foreach (var payment in pendingPayments)
                {
                    bool success = false;
                    string? err = null;

                    if (string.Equals(payment.DebtType, "Customer", StringComparison.OrdinalIgnoreCase))
                    {
                        var res = await mediator.Send(new global::Sales.Application.Sales.Commands.PaySaleInvoice.PaySaleInvoiceCommand(payment.ReferenceId, payment.Amount), ct);
                        success = res.IsSuccess;
                        err = res.IsFailure ? res.Error.Name : null;
                    }
                    else
                    {
                        var res = await mediator.Send(new global::Purchases.Application.Purchases.Commands.PayPurchaseInvoice.PayPurchaseInvoiceCommand(payment.ReferenceId, payment.Amount), ct);
                        success = res.IsSuccess;
                        err = res.IsFailure ? res.Error.Name : null;
                    }

                    if (success)
                    {
                        await _cloudHttp.PostAsync($"api/sync/debts/{payment.Id}/acknowledge", null, ct);
                        count++;
                        _logger.LogInformation("[CloudSync] Successfully applied {DebtType} debt payment {RefId} of amount {Amount}", payment.DebtType, payment.ReferenceId, payment.Amount);
                    }
                    else
                    {
                        _logger.LogWarning("[CloudSync] Failed to apply {DebtType} debt payment {RefId}: {Error}", payment.DebtType, payment.ReferenceId, err);
                        // Acknowledge permanent failures to prevent infinite retry loops
                        bool isPermanentError = err != null && (
                            err.Contains("مسددة بالكامل") ||
                            err.Contains("AlreadyFullyPaid") ||
                            err.Contains("NotFound") ||
                            err.Contains("InvalidPaymentAmount") ||
                            err.Contains("أكبر من") ||
                            err.Contains("يجب أن يكون"));

                        if (isPermanentError)
                        {
                            await _cloudHttp.PostAsync($"api/sync/debts/{payment.Id}/acknowledge", null, ct);
                            count++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[CloudSync] Exception in PullPendingDebtPaymentsFromCloudAsync: {Message}", ex.Message);
            }
            return count;
        }

        private async Task PushReturnsToCloudAsync(IServiceProvider sp, CancellationToken ct)
        {
            try
            {
                var returnsDb = sp.GetService<ReturnsDbContext>();
                if (returnsDb == null) return;

                var salesDb = sp.GetService<SalesDbContext>();
                var purchasesDb = sp.GetService<PurchasesDbContext>();

                var salesReturns = await returnsDb.SalesReturns
                    .AsNoTracking()
                    .Include(sr => sr.Items)
                    .OrderByDescending(sr => sr.ReturnDate)
                    .Take(100)
                    .ToListAsync(ct);

                var purchaseReturns = await returnsDb.PurchaseReturns
                    .AsNoTracking()
                    .Include(pr => pr.Items)
                    .OrderByDescending(pr => pr.ReturnDate)
                    .Take(100)
                    .ToListAsync(ct);

                var itemsToPush = new List<object>();

                if (salesReturns.Any())
                {
                    foreach (var sr in salesReturns)
                    {
                        string? customerName = null;
                        string? originalInvoiceNumber = null;

                        if (salesDb != null)
                        {
                            var originalSale = await salesDb.Sales.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sr.OriginalSaleId, ct);
                            originalInvoiceNumber = originalSale?.InvoiceNumber;
                            if (sr.CustomerId.HasValue)
                            {
                                var cust = await salesDb.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == sr.CustomerId.Value, ct);
                                customerName = cust?.Name;
                            }
                        }

                        var returnItems = sr.Items.Select(i => new
                        {
                            id = i.Id,
                            productId = i.ProductId,
                            productName = "صنف مرتجع",
                            barcode = (string?)null,
                            quantity = i.Quantity,
                            unitPrice = i.UnitPrice,
                            tax = i.Tax,
                            total = i.Total,
                            reason = i.Reason
                        }).ToList();

                        itemsToPush.Add(new
                        {
                            id = sr.Id,
                            returnNumber = sr.ReturnNumber,
                            type = "Sale",
                            originalInvoiceNumber = originalInvoiceNumber,
                            partyName = customerName,
                            partyPhone = (string?)null,
                            returnDate = sr.ReturnDate,
                            totalAmount = sr.TotalAmount,
                            refundMethod = sr.RefundMethod.ToString(),
                            reason = sr.Reason,
                            notes = sr.Notes,
                            itemsJson = JsonSerializer.Serialize(returnItems),
                            itemsCount = returnItems.Count
                        });
                    }
                }

                if (purchaseReturns.Any())
                {
                    foreach (var pr in purchaseReturns)
                    {
                        string? supplierName = null;
                        string? originalInvoiceNumber = null;

                        if (purchasesDb != null)
                        {
                            var originalPurchase = await purchasesDb.Purchases.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pr.OriginalPurchaseId, ct);
                            originalInvoiceNumber = originalPurchase?.InvoiceNumber;
                            if (pr.SupplierId != Guid.Empty)
                            {
                                var sup = await purchasesDb.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == pr.SupplierId, ct);
                                supplierName = sup?.Name;
                            }
                        }

                        var returnItems = pr.Items.Select(i => new
                        {
                            id = i.Id,
                            productId = i.ProductId,
                            productName = "صنف مرتجع",
                            barcode = (string?)null,
                            quantity = i.Quantity,
                            unitPrice = i.UnitCost,
                            tax = i.Tax,
                            total = i.Total,
                            reason = (string?)null
                        }).ToList();

                        itemsToPush.Add(new
                        {
                            id = pr.Id,
                            returnNumber = pr.ReturnNumber,
                            type = "Purchase",
                            originalInvoiceNumber = originalInvoiceNumber,
                            partyName = supplierName,
                            partyPhone = (string?)null,
                            returnDate = pr.ReturnDate,
                            totalAmount = pr.TotalAmount,
                            refundMethod = "Cash",
                            reason = pr.Reason,
                            notes = pr.Notes,
                            itemsJson = JsonSerializer.Serialize(returnItems),
                            itemsCount = returnItems.Count
                        });
                    }
                }

                if (itemsToPush.Any())
                {
                    await _cloudHttp.PostAsJsonAsync("api/sync/returns/push", new { returns = itemsToPush }, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[CloudSync] Error pushing returns: {Message}", ex.Message);
            }
        }
    }

    public record PendingDebtPaymentSyncDto(Guid Id, string DebtType, Guid ReferenceId, decimal Amount, string? Notes, DateTime CreatedAt);
    public record PendingCategorySyncDto(Guid Id, string NameAr, string? NameEn, bool IsActive, DateTime CreatedAt);

    public record PendingSupplierSyncDto(Guid Id, string Name, string? Phone, string? Email, string? Address, string? ContactPerson, decimal Balance = 0);
    public record PendingPurchaseItemSyncDto(Guid Id, Guid ProductId, string ProductName, string Barcode, decimal Quantity, decimal UnitCost, decimal Discount, decimal Tax, decimal Total, DateTime? ExpiryDate, string? BatchNumber, string? Unit);
    public record PendingPurchaseSyncDto(Guid Id, string InvoiceNumber, string? InternalNumber, Guid SupplierId, string SupplierName, DateTime PurchaseDate, decimal SubTotal, decimal DiscountAmount, decimal TaxAmount, decimal TotalAmount, decimal PaidAmount, decimal RemainingAmount, int PaymentMethod, string? Notes, DateTime CreatedAt, List<PendingPurchaseItemSyncDto> Items);
    public record PendingProductSyncDto(Guid Id, string Barcode, string NameAr, string? NameEn, string BaseUnit, string? ParentUnit, int ConversionFactor, decimal PurchasePrice, decimal SellingPrice, decimal WholesalePrice, int ShelfLifeDays, int ExpiryAlertDays, decimal ReorderLevel, bool IsWeighable, bool TrackExpiry, Guid? CategoryId, decimal StockQuantity = 0);
    public record StockUpdateSyncItem(Guid ProductId, decimal NewStockQuantity);
    public record SupplierBalanceSyncItem(Guid SupplierId, decimal NewBalance);
    public record PendingExpenseSyncDto(Guid Id, string Title, decimal Amount, string? Category, DateTime Date, string? Notes);
    public record PendingCloudActionDto(Guid NotificationId, Guid ProductId, Guid? BatchId, string ActionType, decimal Quantity, string? Reason, DateTime? NewExpiryDate, string? NewBatchNumber, string? Notes, DateTime ActionTakenAt);
}
