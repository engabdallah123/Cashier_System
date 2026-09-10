using Inventory.Domain;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Inventory.Application.Notifications.Commands.ResolveExpiryNotification
{
    internal sealed class ResolveExpiryNotificationCommandHandler : ICommandHandler<ResolveExpiryNotificationCommand>
    {
        private readonly IInventoryUnitOfWork _unitOfWork;

        public ResolveExpiryNotificationCommandHandler(IInventoryUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(ResolveExpiryNotificationCommand request, CancellationToken cancellationToken)
        {
            var notification = await _unitOfWork.NotificationRepository.GetByIdAsync(request.NotificationId, cancellationToken);
            if (notification is null)
                return Result.Failure(new Error("Notification.NotFound", "الإشعار غير موجود."));

            // [كله تمام] - تم حل ومراجعة التنبيه بنجاح، دون أي تعديل على رصيد المخزون
            notification.Resolve(request.UserId);
            _unitOfWork.NotificationRepository.Update(notification);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}
