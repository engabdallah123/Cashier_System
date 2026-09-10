using Inventory.Domain;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Inventory.Application.Notifications.Commands.SnoozeExpiryNotification
{
    internal sealed class SnoozeExpiryNotificationCommandHandler : ICommandHandler<SnoozeExpiryNotificationCommand>
    {
        private readonly IInventoryUnitOfWork _unitOfWork;

        public SnoozeExpiryNotificationCommandHandler(IInventoryUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(SnoozeExpiryNotificationCommand request, CancellationToken cancellationToken)
        {
            var notification = await _unitOfWork.NotificationRepository.GetByIdAsync(request.NotificationId, cancellationToken);
            if (notification is null)
                return Result.Failure(new Error("Notification.NotFound", "الإشعار غير موجود."));

            // [Skip] - تأجيل الإشعار لمدة 24 ساعة، دون أي تعديل على رصيد المخزون
            notification.Snooze(request.Hours > 0 ? request.Hours : 24);
            _unitOfWork.NotificationRepository.Update(notification);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}
