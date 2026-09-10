using Inventory.Domain.Notifications.Entities;

namespace Inventory.Domain.Notifications.Interface
{
    public interface IExpiryNotificationRepository
    {
        Task<ExpiryNotification?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<ExpiryNotification?> GetByBatchIdAsync(Guid batchId, string type = "ExpiryAlert", CancellationToken ct = default);
        Task<IReadOnlyList<ExpiryNotification>> GetActiveAndDueNotificationsAsync(CancellationToken ct = default);
        Task<IReadOnlyList<ExpiryNotification>> GetAllNotificationsAsync(CancellationToken ct = default);
        Task AddAsync(ExpiryNotification notification, CancellationToken ct = default);
        void Update(ExpiryNotification notification);
    }
}
