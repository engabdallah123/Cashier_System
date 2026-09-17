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
    [Route("api/cloud/suppliers")]
    [Authorize]
    public class CloudSuppliersController : ControllerBase
    {
        private readonly CloudDbContext _db;

        public CloudSuppliersController(CloudDbContext db)
        {
            _db = db;
        }

        private Guid GetTenantId()
        {
            var tenantClaim = User.FindFirstValue("TenantId");
            return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : Guid.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _db.Suppliers
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId && s.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(s => s.Name.ToLower().Contains(term) || (s.Phone != null && s.Phone.Contains(term)));
            }

            var suppliers = await query
                .OrderBy(s => s.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new CloudSupplierDto(
                    s.Id,
                    s.Name,
                    s.Phone,
                    s.Email,
                    s.Address,
                    s.ContactPerson,
                    s.Balance,
                    s.IsActive,
                    s.SyncStatus.ToString()
                ))
                .ToListAsync();

            return Ok(suppliers);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCloudSupplierRequest req)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { message = "اسم المورد مطلوب." });

            if (string.IsNullOrWhiteSpace(req.Phone))
                return BadRequest(new { message = "رقم هاتف المورد مطلوب." });

            var supplier = new CloudSupplier
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = req.Name.Trim(),
                Phone = req.Phone.Trim(),
                Email = req.Email?.Trim(),
                Address = req.Address?.Trim(),
                ContactPerson = req.ContactPerson?.Trim(),
                Balance = req.Balance,
                IsActive = true,
                SyncStatus = SyncStatus.PendingSync,
                CreatedAt = DateTime.UtcNow
            };

            _db.Suppliers.Add(supplier);
            await _db.SaveChangesAsync();

            return Ok(new CloudSupplierDto(
                supplier.Id,
                supplier.Name,
                supplier.Phone,
                supplier.Email,
                supplier.Address,
                supplier.ContactPerson,
                supplier.Balance,
                supplier.IsActive,
                supplier.SyncStatus.ToString()
            ));
        }
    }
}
