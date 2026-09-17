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
    [Route("api/cloud/categories")]
    [Authorize]
    public class CloudCategoriesController : ControllerBase
    {
        private readonly CloudDbContext _db;

        public CloudCategoriesController(CloudDbContext db)
        {
            _db = db;
        }

        private Guid GetTenantId()
        {
            var tenantClaim = User.FindFirstValue("TenantId");
            return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : Guid.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            var categories = await _db.Categories
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .OrderBy(c => c.NameAr)
                .Select(c => new CloudCategoryDto(
                    c.Id,
                    c.NameAr,
                    c.NameEn,
                    c.IsActive,
                    c.SyncStatus.ToString()
                ))
                .ToListAsync();

            return Ok(categories);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCloudCategoryRequest req)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            if (string.IsNullOrWhiteSpace(req.NameAr))
                return BadRequest(new { message = "اسم التصنيف مطلوب." });

            var category = new CloudCategory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                NameAr = req.NameAr.Trim(),
                NameEn = req.NameEn?.Trim(),
                IsActive = true,
                SyncStatus = SyncStatus.PendingSync,
                CreatedAt = DateTime.UtcNow
            };

            _db.Categories.Add(category);
            await _db.SaveChangesAsync();

            return Ok(new CloudCategoryDto(
                category.Id,
                category.NameAr,
                category.NameEn,
                category.IsActive,
                category.SyncStatus.ToString()
            ));
        }
    }
}
