using POS.Shared.Application.Messaging;

namespace Inventory.Application.Notifications.Queries.GetExpiryNotifications
{
    public sealed record GetExpiryNotificationsQuery(bool IncludeSnoozed = false) : IQuery<IReadOnlyList<ExpiryNotificationDto>>;
}
