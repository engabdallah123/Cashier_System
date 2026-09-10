using POS.Shared.Application.Messaging;

namespace Inventory.Application.Notifications.Commands.SnoozeExpiryNotification
{
    public sealed record SnoozeExpiryNotificationCommand(
        Guid NotificationId,
        int Hours = 24) : ICommand;
}
