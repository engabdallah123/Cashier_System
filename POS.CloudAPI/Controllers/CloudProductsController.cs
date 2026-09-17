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
    [Route("api/cloud/products")]
    [Authorize]
    public class CloudProductsController : ControllerBase
    {
        private readonly CloudDbContext _db;

        public CloudProductsController(CloudDbContext db)
        {
            _db = db;
        }

        private Guid GetTenantId()
        {
            var tenantClaim = User.FindFirstValue("TenantId");
            return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : Guid.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts(
            [FromQuery] string? search,
            [FromQuery] Guid? categoryId,
            [FromQuery] bool? lowStockOnly,
            [FromQuery] bool? weighableOnly,
            [FromQuery] bool? expiryTrackedOnly,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            var query = _db.Products
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.IsActive);

            if (categoryId.HasValue && categoryId.Value != Guid.Empty)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            if (lowStockOnly == true)
            {
                query = query.Where(p => p.StockQuantity <= p.ReorderLevel);
            }

            if (weighableOnly == true)
            {
                query = query.Where(p => p.IsWeighable);
            }

            if (expiryTrackedOnly == true)
            {
                query = query.Where(p => p.TrackExpiry);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p =>
                    p.Barcode.Contains(term) ||
                    p.NameAr.Contains(term) ||
                    (p.NameEn != null && p.NameEn.Contains(term)));
            }

            var products = await query
                .OrderBy(p => p.NameAr)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new CloudProductDto(
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
                ))
                .ToListAsync();

            return Ok(products);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            var product = await _db.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id);

            if (product == null) return NotFound();

            return Ok(new CloudProductDto(
                product.Id,
                product.Barcode,
                product.NameAr,
                product.NameEn,
                product.BaseUnit,
                product.ParentUnit,
                product.ConversionFactor,
                product.PurchasePrice,
                product.SellingPrice,
                product.WholesalePrice,
                product.StockQuantity,
                product.CategoryId,
                product.CategoryName,
                product.IsWeighable,
                product.ShelfLifeDays,
                product.ExpiryAlertDays,
                product.ReorderLevel,
                product.TrackExpiry,
                product.IsActive,
                product.SyncStatus.ToString()
            ));
        }

        [HttpGet("barcode/{barcode}")]
        public async Task<IActionResult> GetByBarcode(string barcode)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            if (string.IsNullOrWhiteSpace(barcode))
                return BadRequest();

            var product = await _db.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Barcode == barcode.Trim() && p.IsActive);

            if (product == null)
                return NotFound(new { message = "المنتج غير موجود في الكتالوج." });

            return Ok(new CloudProductDto(
                product.Id,
                product.Barcode,
                product.NameAr,
                product.NameEn,
                product.BaseUnit,
                product.ParentUnit,
                product.ConversionFactor,
                product.PurchasePrice,
                product.SellingPrice,
                product.WholesalePrice,
                product.StockQuantity,
                product.CategoryId,
                product.CategoryName,
                product.IsWeighable,
                product.ShelfLifeDays,
                product.ExpiryAlertDays,
                product.ReorderLevel,
                product.TrackExpiry,
                product.IsActive,
                product.SyncStatus.ToString()
            ));
        }

        [HttpPost]
        public async Task<IActionResult> CreateProduct([FromBody] CreateCloudProductRequest req)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            if (string.IsNullOrWhiteSpace(req.NameAr) || string.IsNullOrWhiteSpace(req.Barcode))
                return BadRequest(new { message = "اسم المنتج والباركود مطلوبان." });

            var existing = await _db.Products.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Barcode == req.Barcode.Trim());
            if (existing != null)
            {
                return BadRequest(new { message = "يوجد منتج آخر مسجل بنفس هذا الباركود مسبقاً." });
            }

            var product = new CloudProduct
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Barcode = req.Barcode.Trim(),
                NameAr = req.NameAr.Trim(),
                NameEn = req.NameEn?.Trim(),
                BaseUnit = string.IsNullOrWhiteSpace(req.BaseUnit) ? "قطعة" : req.BaseUnit.Trim(),
                ParentUnit = string.IsNullOrWhiteSpace(req.ParentUnit) ? "كرتونة" : req.ParentUnit.Trim(),
                ConversionFactor = req.ConversionFactor > 0 ? req.ConversionFactor : 1,
                PurchasePrice = req.PurchasePrice,
                SellingPrice = req.SellingPrice,
                WholesalePrice = req.WholesalePrice,
                StockQuantity = req.InitialStock,
                CategoryId = req.CategoryId,
                CategoryName = req.CategoryName?.Trim(),
                IsWeighable = req.IsWeighable,
                ShelfLifeDays = req.ShelfLifeDays,
                ExpiryAlertDays = req.ExpiryAlertDays > 0 ? req.ExpiryAlertDays : 3,
                ReorderLevel = req.ReorderLevel,
                TrackExpiry = req.TrackExpiry,
                IsActive = true,
                SyncStatus = SyncStatus.PendingSync, // Mark as PendingSync so Desktop pulls and creates it locally!
                UpdatedAt = DateTime.UtcNow
            };

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            return Ok(new CloudProductDto(
                product.Id,
                product.Barcode,
                product.NameAr,
                product.NameEn,
                product.BaseUnit,
                product.ParentUnit,
                product.ConversionFactor,
                product.PurchasePrice,
                product.SellingPrice,
                product.WholesalePrice,
                product.StockQuantity,
                product.CategoryId,
                product.CategoryName,
                product.IsWeighable,
                product.ShelfLifeDays,
                product.ExpiryAlertDays,
                product.ReorderLevel,
                product.TrackExpiry,
                product.IsActive,
                product.SyncStatus.ToString()
            ));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateCloudProductRequest req)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            var product = await _db.Products.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id);
            if (product == null) return NotFound(new { message = "المنتج غير موجود." });

            product.Barcode = req.Barcode.Trim();
            product.NameAr = req.NameAr.Trim();
            product.NameEn = req.NameEn?.Trim();
            product.BaseUnit = string.IsNullOrWhiteSpace(req.BaseUnit) ? "قطعة" : req.BaseUnit.Trim();
            product.ParentUnit = req.ParentUnit?.Trim();
            product.ConversionFactor = req.ConversionFactor > 0 ? req.ConversionFactor : 1;
            product.PurchasePrice = req.PurchasePrice;
            product.SellingPrice = req.SellingPrice;
            product.WholesalePrice = req.WholesalePrice;
            product.CategoryId = req.CategoryId;
            product.CategoryName = req.CategoryName?.Trim();
            product.IsWeighable = req.IsWeighable;
            product.ShelfLifeDays = req.ShelfLifeDays;
            product.ExpiryAlertDays = req.ExpiryAlertDays > 0 ? req.ExpiryAlertDays : 3;
            product.ReorderLevel = req.ReorderLevel;
            product.TrackExpiry = req.TrackExpiry;
            product.SyncStatus = SyncStatus.PendingSync; // Mark modified so desktop updates local copy
            product.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new CloudProductDto(
                product.Id,
                product.Barcode,
                product.NameAr,
                product.NameEn,
                product.BaseUnit,
                product.ParentUnit,
                product.ConversionFactor,
                product.PurchasePrice,
                product.SellingPrice,
                product.WholesalePrice,
                product.StockQuantity,
                product.CategoryId,
                product.CategoryName,
                product.IsWeighable,
                product.ShelfLifeDays,
                product.ExpiryAlertDays,
                product.ReorderLevel,
                product.TrackExpiry,
                product.IsActive,
                product.SyncStatus.ToString()
            ));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            var product = await _db.Products.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id);
            if (product == null) return NotFound();

            product.IsActive = false;
            product.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { success = true, message = "تم إلغاء تفعيل المنتج بنجاح." });
        }
    }
}
