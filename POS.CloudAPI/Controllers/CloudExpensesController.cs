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
    [Route("api/cloud/expenses")]
    [Authorize]
    public class CloudExpensesController : ControllerBase
    {
        private readonly CloudDbContext _db;

        public CloudExpensesController(CloudDbContext db)
        {
            _db = db;
        }

        private Guid GetTenantId()
        {
            var tenantClaim = User.FindFirstValue("TenantId");
            return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : Guid.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> GetExpenses(
            [FromQuery] int? month,
            [FromQuery] int? year,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var now = DateTime.UtcNow;
            var targetMonth = month ?? now.Month;
            var targetYear = year ?? now.Year;

            var startDate = new DateTime(targetYear, targetMonth, 1);
            var endDate = startDate.AddMonths(1);

            var baseQuery = _db.Expenses
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId && e.Date >= startDate && e.Date < endDate);

            var totalAmount = await baseQuery.SumAsync(e => e.Amount);
            var totalCount = await baseQuery.CountAsync();

            var expenses = await baseQuery
                .OrderByDescending(e => e.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new CloudExpenseDto(
                    e.Id,
                    e.Title,
                    e.Amount,
                    e.Category,
                    e.Date,
                    e.Notes,
                    e.SyncStatus.ToString()
                ))
                .ToListAsync();

            return Ok(new
            {
                Month = targetMonth,
                Year = targetYear,
                TotalAmount = totalAmount,
                Count = totalCount,
                Page = page,
                PageSize = pageSize,
                HasMore = (page * pageSize) < totalCount,
                Items = expenses
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCloudExpenseRequest req)
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Title) || req.Amount <= 0)
                return BadRequest(new { message = "بيانات المصروف غير مكتملة." });

            var expense = new CloudExpense
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Title = req.Title.Trim(),
                Amount = req.Amount,
                Category = req.Category?.Trim() ?? "عام",
                Date = req.Date ?? DateTime.UtcNow,
                Notes = req.Notes?.Trim(),
                SyncStatus = SyncStatus.PendingSync,
                CreatedAt = DateTime.UtcNow
            };

            _db.Expenses.Add(expense);
            await _db.SaveChangesAsync();

            return Ok(new CloudExpenseDto(
                expense.Id,
                expense.Title,
                expense.Amount,
                expense.Category,
                expense.Date,
                expense.Notes,
                expense.SyncStatus.ToString()
            ));
        }
    }
}
