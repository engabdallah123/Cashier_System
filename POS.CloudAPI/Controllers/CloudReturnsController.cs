using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.CloudAPI.Database;
using POS.CloudAPI.DTOs;
using System.Security.Claims;

namespace POS.CloudAPI.Controllers
{
    [ApiController]
    [Route("api/cloud/returns")]
    [Authorize]
    public class CloudReturnsController : ControllerBase
    {
        private readonly CloudDbContext _db;

        public CloudReturnsController(CloudDbContext db)
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
            [FromQuery] string? type,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            var query = _db.Returns
                .AsNoTracking()
                .Where(r => r.TenantId == tenantId);

            if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => r.Type == type);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(r =>
                    r.ReturnNumber.Contains(term) ||
                    (r.OriginalInvoiceNumber != null && r.OriginalInvoiceNumber.Contains(term)) ||
                    (r.PartyName != null && r.PartyName.Contains(term)));
            }

            var totalCount = await query.CountAsync();
            var totalAmount = await query.SumAsync(r => r.TotalAmount);

            var items = await query
                .OrderByDescending(r => r.ReturnDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new CloudReturnDto(
                    r.Id,
                    r.ReturnNumber,
                    r.Type,
                    r.OriginalInvoiceNumber,
                    r.PartyName,
                    r.PartyPhone,
                    r.ReturnDate,
                    r.TotalAmount,
                    r.RefundMethod,
                    r.Reason,
                    r.Notes,
                    r.ItemsJson,
                    r.ItemsCount,
                    r.CreatedAt
                ))
                .ToListAsync();

            return Ok(new
            {
                totalCount,
                totalAmount,
                page,
                pageSize,
                returns = items,
                items = items
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            var ret = await _db.Returns
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId);

            if (ret == null) return NotFound(new { message = "سجل المرتجع غير موجود." });

            return Ok(new CloudReturnDto(
                ret.Id,
                ret.ReturnNumber,
                ret.Type,
                ret.OriginalInvoiceNumber,
                ret.PartyName,
                ret.PartyPhone,
                ret.ReturnDate,
                ret.TotalAmount,
                ret.RefundMethod,
                ret.Reason,
                ret.Notes,
                ret.ItemsJson,
                ret.ItemsCount,
                ret.CreatedAt
            ));
        }
    }
}
