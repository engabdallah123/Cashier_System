using Inventory.Domain.Notifications.Entities;
using Inventory.Domain.Notifications.Interface;
using Inventory.Infrastructre.Database;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructre.Repositories.Notifications
{
    public class ExpiryNotificationRepository : IExpiryNotificationRepository
    {
        private readonly InventoryDbContext _context;

        public ExpiryNotificationRepository(InventoryDbContext context)
        {
            _context = context;
        }

        public async Task<ExpiryNotification?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);
        }

        public async Task<ExpiryNotification?> GetByBatchIdAsync(Guid batchId, string type = "ExpiryAlert", CancellationToken ct = default)
        {
            return await _context.Notifications
                .FirstOrDefaultAsync(n => n.BatchId == batchId && n.Type == type, ct);
        }

        public async Task<IReadOnlyList<ExpiryNotification>> GetActiveAndDueNotificationsAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            return await _context.Notifications
                .Where(n => n.Status == "Active" || (n.Status == "Snoozed" && n.SnoozedUntil <= now))
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<ExpiryNotification>> GetAllNotificationsAsync(CancellationToken ct = default)
        {
            return await _context.Notifications
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task AddAsync(ExpiryNotification notification, CancellationToken ct = default)
        {
            await _context.Notifications.AddAsync(notification, ct);
        }

        public void Update(ExpiryNotification notification)
        {
            _context.Notifications.Update(notification);
        }
    }
}
