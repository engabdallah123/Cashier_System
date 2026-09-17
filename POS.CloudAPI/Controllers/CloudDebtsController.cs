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
    [Route("api/cloud/debts")]
    [Authorize]
    public class CloudDebtsController : ControllerBase
    {
        private readonly CloudDbContext _db;

        public CloudDebtsController(CloudDbContext db)
        {
            _db = db;
        }

        private Guid GetTenantId()
        {
            var tenantClaim = User.FindFirstValue("TenantId");
            return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : Guid.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> GetDebts(
            [FromQuery] string? type,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _db.DebtItems
                .AsNoTracking()
                .Where(d => d.TenantId == tenantId && d.RemainingAmount > 0);

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(d => d.Type == type);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(d =>
                    d.EntityName.Contains(term) ||
                    d.InvoiceNumber.Contains(term) ||
                    (d.Phone != null && d.Phone.Contains(term)));
            }

            var totalFilteredCount = await query.CountAsync();

            var debts = await query
                .OrderByDescending(d => d.RemainingAmount)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new CloudDebtDto(
                    d.Id,
                    d.Type,
                    d.ReferenceId,
                    d.InvoiceNumber,
                    d.EntityName,
                    d.Phone,
                    d.TotalAmount,
                    d.PaidAmount,
                    d.RemainingAmount,
                    d.Date
                ))
                .ToListAsync();

            var customerTotal = await _db.DebtItems
                .Where(d => d.TenantId == tenantId && d.Type == "Customer" && d.RemainingAmount > 0)
                .SumAsync(d => d.RemainingAmount);

            var supplierTotal = await _db.DebtItems
                .Where(d => d.TenantId == tenantId && d.Type == "Supplier" && d.RemainingAmount > 0)
                .SumAsync(d => d.RemainingAmount);

            return Ok(new
            {
                TotalCustomerDebts = customerTotal,
                TotalSupplierDebts = supplierTotal,
                NetBalance = customerTotal - supplierTotal,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalFilteredCount,
                HasMore = (page * pageSize) < totalFilteredCount,
                Items = debts
            });
        }

        [HttpPost("pay")]
        public async Task<IActionResult> PayDebt([FromBody] PayDebtRequest req)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            if (req.Amount <= 0)
                return BadRequest(new { message = "مبلغ السداد يجب أن يكون أكبر من الصفر." });

            var payment = new CloudDebtPayment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DebtType = req.DebtType,
                ReferenceId = req.ReferenceId,
                Amount = req.Amount,
                Notes = req.Notes,
                SyncStatus = SyncStatus.PendingSync,
                CreatedAt = DateTime.UtcNow
            };

            _db.DebtPayments.Add(payment);

            // Optimistically decrease remaining amount on CloudDebtItem if found
            var debtItem = await _db.DebtItems.FirstOrDefaultAsync(d =>
                d.TenantId == tenantId &&
                d.Type == req.DebtType &&
                d.ReferenceId == req.ReferenceId);

            if (debtItem != null)
            {
                debtItem.PaidAmount += req.Amount;
                debtItem.RemainingAmount = Math.Max(0, debtItem.RemainingAmount - req.Amount);
                debtItem.UpdatedAt = DateTime.UtcNow;

                if (string.Equals(req.DebtType, "Supplier", StringComparison.OrdinalIgnoreCase))
                {
                    var supplier = await _db.Suppliers.FirstOrDefaultAsync(s =>
                        s.TenantId == tenantId &&
                        (s.Name == debtItem.EntityName || (debtItem.Phone != null && s.Phone == debtItem.Phone)));
                    if (supplier != null)
                    {
                        supplier.Balance = Math.Max(0, supplier.Balance - req.Amount);
                        supplier.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "تم تسجيل طلب السداد بنجاح وجاري مزامنته مع كاشير المحل.",
                paymentId = payment.Id
            });
        }
    }
}
