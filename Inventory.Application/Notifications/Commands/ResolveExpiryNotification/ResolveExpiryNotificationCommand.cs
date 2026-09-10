using POS.Shared.Application.Messaging;

namespace Inventory.Application.Notifications.Commands.ResolveExpiryNotification
{
    public sealed record ResolveExpiryNotificationCommand(
        Guid NotificationId,
        Guid? UserId = null) : ICommand;
}
