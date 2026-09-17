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
    [Route("api/cloud/notifications")]
    [Authorize]
    public class CloudNotificationsController : ControllerBase
    {
        private readonly CloudDbContext _db;

        public CloudNotificationsController(CloudDbContext db)
        {
            _db = db;
        }

        private Guid GetTenantId()
        {
            var tenantClaim = User.FindFirstValue("TenantId");
            return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : Guid.Empty;
        }

        // 1. Get Expiry Notifications
        [HttpGet("expiry")]
        public async Task<IActionResult> GetExpiryNotifications([FromQuery] bool includeResolved = false)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            var query = _db.ExpiryNotifications
                .AsNoTracking()
                .Where(n => n.TenantId == tenantId);

            if (!includeResolved)
            {
                query = query.Where(n => n.Status == "Active");
            }

            var list = await query
                .OrderBy(n => n.DaysRemaining)
                .Select(n => new CloudExpiryNotificationItemDto(
                    n.Id,
                    n.ProductId,
                    n.ProductName,
                    n.Barcode,
                    n.BatchId,
                    n.BatchNumber,
                    n.RemainingQuantity,
                    n.Unit,
                    n.UnitCost,
                    n.ExpiryDate,
                    n.DaysRemaining,
                    n.IsExpired,
                    n.Message,
                    n.Status
                ))
                .ToListAsync();

            return Ok(list);
        }

        // 2. Action on Expiry Notification (OK, Waste, SupplierReplacement)
        [HttpPost("{id:guid}/action")]
        public async Task<IActionResult> TakeAction(Guid id, [FromBody] NotificationActionRequest req)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            var notif = await _db.ExpiryNotifications.FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId);
            if (notif == null) return NotFound(new { message = "الإشعار غير موجود." });

            notif.ActionType = req.ActionType;
            notif.ActionQuantity = req.Quantity;
            notif.ActionReason = req.Reason;
            notif.NewExpiryDate = req.NewExpiryDate;
            notif.NewBatchNumber = req.NewBatchNumber;
            notif.ActionNotes = req.Notes;
            notif.ActionTakenAt = DateTime.UtcNow;
            notif.Status = "Resolved";
            notif.SyncStatus = SyncStatus.PendingSync; // Needs to be pulled and applied on Desktop

            await _db.SaveChangesAsync();

            return Ok(new { success = true, message = "تم تسجيل الإجراء وجاري مزامنته مع الكاشير." });
        }

        // 3. Low Stock Alerts
        [HttpGet("low-stock")]
        public async Task<IActionResult> GetLowStockAlerts()
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            var lowStockProducts = await _db.Products
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.IsActive && (p.StockQuantity <= p.ReorderLevel || p.StockQuantity <= 0 || (p.ReorderLevel <= 0 && p.StockQuantity <= 5)))
                .OrderBy(p => p.StockQuantity)
                .Take(50)
                .Select(p => new
                {
                    p.Id,
                    p.Barcode,
                    p.NameAr,
                    p.StockQuantity,
                    p.ReorderLevel,
                    p.BaseUnit,
                    p.PurchasePrice,
                    p.SellingPrice,
                    p.CategoryName
                })
                .ToListAsync();

            return Ok(lowStockProducts);
        }

        // 4. Closed Shifts / Cashier Performance Alerts
        [HttpGet("shifts")]
        public async Task<IActionResult> GetRecentShiftSummaries([FromQuery] int take = 10)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            var shifts = await _db.ShiftSummaries
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId)
                .OrderByDescending(s => s.ClosedAt)
                .Take(take)
                .Select(s => new CloudShiftSummaryDto(
                    s.Id,
                    s.ShiftId,
                    s.CashierName,
                    s.OpenedAt,
                    s.ClosedAt,
                    s.OpeningCash,
                    s.ActualClosingCash,
                    s.SystemCash,
                    s.CashDifference,
                    s.TotalSales,
                    s.TotalCash,
                    s.TotalCard,
                    s.TotalWallet,
                    s.TotalCredit,
                    s.TotalInvoices,
                    s.TotalReturns,
                    s.ClosingNotes,
                    s.IsReadByOwner,
                    s.CreatedAt
                ))
                .ToListAsync();

            return Ok(shifts);
        }

        // 5. Mark Shift Summary as Read
        [HttpPost("shifts/{id:guid}/read")]
        public async Task<IActionResult> MarkShiftRead(Guid id)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            var shift = await _db.ShiftSummaries.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);
            if (shift != null)
            {
                shift.IsReadByOwner = true;
                await _db.SaveChangesAsync();
            }

            return Ok(new { success = true });
        }
    }
}
