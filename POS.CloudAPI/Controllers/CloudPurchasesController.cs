using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.CloudAPI.Database;
using POS.CloudAPI.DTOs;
using POS.CloudAPI.Entities;
using System.Security.Claims;

namespace POS.CloudAPI.Controllers
{
    [ApiController]
    [Route("api/cloud/purchases")]
    [Authorize]
    public class CloudPurchasesController : ControllerBase
    {
        private readonly CloudDbContext _db;

        public CloudPurchasesController(CloudDbContext db)
        {
            _db = db;
        }

        private Guid GetTenantId()
        {
            var tenantClaim = User.FindFirstValue("TenantId");
            return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : Guid.Empty;
        }

        private Guid GetUserId()
        {
            var userClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(userClaim, out var userId) ? userId : Guid.Empty;
        }

        private string GetUserName()
        {
            return User.FindFirstValue("FullName") ?? User.Identity?.Name ?? "مستخدم الموبايل";
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? status,
            [FromQuery] Guid? supplierId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            var query = _db.Purchases
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId);

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SyncStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(p => p.SyncStatus == parsedStatus);
            }

            if (supplierId.HasValue && supplierId.Value != Guid.Empty)
            {
                query = query.Where(p => p.SupplierId == supplierId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p =>
                    p.InvoiceNumber.Contains(term) ||
                    (p.SupplierName != null && p.SupplierName.Contains(term)));
            }

            var totalCount = await query.CountAsync();

            var purchases = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new CloudPurchaseSummaryDto(
                    p.Id,
                    p.InvoiceNumber,
                    p.InternalNumber,
                    p.SupplierId,
                    p.SupplierName,
                    p.PurchaseDate,
                    p.TotalAmount,
                    p.PaidAmount,
                    p.RemainingAmount,
                    (int)p.PaymentMethod,
                    p.Notes,
                    p.SyncStatus.ToString(),
                    p.SyncedAt,
                    p.SyncError,
                    p.CreatedAt,
                    p.Items.Count
                ))
                .ToListAsync();

            return Ok(new
            {
                Total = totalCount,
                Page = page,
                PageSize = pageSize,
                Items = purchases
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            var p = await _db.Purchases
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);

            if (p == null)
                return NotFound(new { message = "فاتورة الشراء غير موجودة." });

            var response = new CloudPurchaseDetailDto(
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
            );

            return Ok(response);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCloudPurchaseRequest req)
        {
            var tenantId = GetTenantId();
            var userId = GetUserId();
            var userName = GetUserName();

            if (tenantId == Guid.Empty) return Unauthorized();

            // Validation
            if (string.IsNullOrWhiteSpace(req.InvoiceNumber))
                return BadRequest(new { message = "رقم الفاتورة مطلوب." });

            if (req.SupplierId == Guid.Empty)
                return BadRequest(new { message = "يجب اختيار المورد." });

            if (req.Items == null || !req.Items.Any())
                return BadRequest(new { message = "يجب إضافة صنف واحد على الأقل في الفاتورة." });

            // 1. Idempotency check: if client sent an Id and it already exists, return it!
            var purchaseId = req.Id.HasValue && req.Id.Value != Guid.Empty ? req.Id.Value : Guid.NewGuid();

            var existing = await _db.Purchases
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == purchaseId && p.TenantId == tenantId);

            if (existing != null)
            {
                // Already created (duplicate request prevented)
                return Ok(new
                {
                    message = "تم تسجيل الفاتورة بالفعل مسبقاً (منع التكرار).",
                    purchaseId = existing.Id,
                    syncStatus = existing.SyncStatus.ToString()
                });
            }

            // Lookup supplier name
            var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == req.SupplierId && s.TenantId == tenantId);

            decimal subTotal = 0;
            var itemsList = new List<CloudPurchaseItem>();

            foreach (var itemReq in req.Items)
            {
                if (itemReq.Quantity <= 0)
                    return BadRequest(new { message = $"الكمية غير صحيحة للصنف {itemReq.ProductName}" });

                if (itemReq.UnitCost < 0)
                    return BadRequest(new { message = $"سعر التكلفة غير صحيح للصنف {itemReq.ProductName}" });

                var itemTotal = (itemReq.Quantity * itemReq.UnitCost) - itemReq.Discount + itemReq.Tax;
                subTotal += itemTotal;

                itemsList.Add(new CloudPurchaseItem
                {
                    Id = Guid.NewGuid(),
                    PurchaseId = purchaseId,
                    ProductId = itemReq.ProductId,
                    ProductName = itemReq.ProductName.Trim(),
                    Barcode = itemReq.Barcode?.Trim(),
                    Quantity = itemReq.Quantity,
                    UnitCost = itemReq.UnitCost,
                    Discount = itemReq.Discount,
                    Tax = itemReq.Tax,
                    Total = itemTotal,
                    ExpiryDate = itemReq.ExpiryDate,
                    BatchNumber = itemReq.BatchNumber?.Trim(),
                    Unit = itemReq.Unit?.Trim()
                });
            }

            var totalAmount = subTotal - req.DiscountAmount + req.TaxAmount;
            var remainingAmount = totalAmount - req.PaidAmount;

            var purchase = new CloudPurchase
            {
                Id = purchaseId,
                TenantId = tenantId,
                InvoiceNumber = req.InvoiceNumber.Trim(),
                InternalNumber = req.InternalNumber?.Trim(),
                SupplierId = req.SupplierId,
                SupplierName = supplier?.Name ?? "مورد عام",
                PurchaseDate = req.PurchaseDate ?? DateTime.UtcNow,
                SubTotal = subTotal,
                DiscountAmount = req.DiscountAmount,
                TaxAmount = req.TaxAmount,
                TotalAmount = totalAmount,
                PaidAmount = req.PaidAmount,
                RemainingAmount = remainingAmount,
                PaymentMethod = (PurchasePaymentMethod)req.PaymentMethod,
                Notes = req.Notes?.Trim(),
                CreatedByUserId = userId,
                CreatedByName = userName,
                SyncStatus = SyncStatus.PendingSync, // Critical requirement: PendingSync by default
                SyncAttempts = 0,
                SyncError = null,
                CreatedAt = DateTime.UtcNow,
                Items = itemsList
            };

            _db.Purchases.Add(purchase);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                purchaseId = purchase.Id,
                invoiceNumber = purchase.InvoiceNumber,
                syncStatus = purchase.SyncStatus.ToString(),
                totalAmount = purchase.TotalAmount,
                message = "تم حفظ فاتورة المشتريات بنجاح في السحابة وحالتها [قيد المزامنة]."
            });
        }

        [HttpPost("{id:guid}/retry")]
        public async Task<IActionResult> RetrySync(Guid id)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            var purchase = await _db.Purchases.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
            if (purchase == null)
                return NotFound(new { message = "الفاتورة غير موجودة." });

            purchase.SyncStatus = SyncStatus.PendingSync;
            purchase.SyncError = null;
            await _db.SaveChangesAsync();

            return Ok(new { message = "تمت إعادة تعيين الفاتورة إلى [قيد المزامنة].", syncStatus = "PendingSync" });
        }
    }
}
