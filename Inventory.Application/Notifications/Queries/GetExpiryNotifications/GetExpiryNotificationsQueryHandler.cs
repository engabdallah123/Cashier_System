using Inventory.Domain;
using Inventory.Domain.Notifications.Entities;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Inventory.Application.Notifications.Queries.GetExpiryNotifications
{
    internal sealed class GetExpiryNotificationsQueryHandler : IQueryHandler<GetExpiryNotificationsQuery, IReadOnlyList<ExpiryNotificationDto>>
    {
        private readonly IInventoryUnitOfWork _unitOfWork;

        public GetExpiryNotificationsQueryHandler(IInventoryUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<IReadOnlyList<ExpiryNotificationDto>>> Handle(GetExpiryNotificationsQuery request, CancellationToken cancellationToken)
        {
            // 1. فحص الدفعات النشطة واكتشاف الأصناف التي اقتربت من أو تجاوزت الصلاحية
            var activeBatches = await _unitOfWork.BatchRepository.GetAllActiveBatchesAsync(cancellationToken);
            var today = DateTime.Today;
            bool newNotificationsCreated = false;

            foreach (var batch in activeBatches)
            {
                if (!batch.ExpiryDate.HasValue || batch.RemainingQuantity <= 0)
                    continue;

                var product = await _unitOfWork.ProductRepository.GetByIdAsync(batch.ProductId, cancellationToken);
                if (product is null)
                    continue;

                int daysRemaining = (int)(batch.ExpiryDate.Value.Date - today).TotalDays;
                int alertThreshold = product.ExpiryAlertDays > 0 ? product.ExpiryAlertDays : 3;

                if (daysRemaining <= alertThreshold)
                {
                    var existing = await _unitOfWork.NotificationRepository.GetByBatchIdAsync(batch.Id, "ExpiryAlert", cancellationToken);
                    if (existing is null)
                    {
                        string msg = daysRemaining < 0
                            ? $"انتهت صلاحية الدفعة '{batch.BatchNumber}' للمنتج '{product.NameAr}' منذ {Math.Abs(daysRemaining)} يوم (المتبقي: {batch.RemainingQuantity} {product.BaseUnit})"
                            : (daysRemaining == 0
                                ? $"تنتهي صلاحية الدفعة '{batch.BatchNumber}' للمنتج '{product.NameAr}' اليوم (المتبقي: {batch.RemainingQuantity} {product.BaseUnit})"
                                : $"الدفعة '{batch.BatchNumber}' للمنتج '{product.NameAr}' ستنتهي بعد {daysRemaining} يوم (المتبقي: {batch.RemainingQuantity} {product.BaseUnit})");

                        var notifResult = ExpiryNotification.Create(
                            productId: product.Id,
                            batchId: batch.Id,
                            message: msg,
                            remainingQuantity: batch.RemainingQuantity,
                            daysRemaining: daysRemaining,
                            type: "ExpiryAlert");

                        if (notifResult.IsSuccess)
                        {
                            await _unitOfWork.NotificationRepository.AddAsync(notifResult.Value!, cancellationToken);
                            newNotificationsCreated = true;
                        }
                    }
                }
            }

            if (newNotificationsCreated)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // 2. استرجاع الإشعارات النشطة والمستحقة
            var notifications = await _unitOfWork.NotificationRepository.GetActiveAndDueNotificationsAsync(cancellationToken);
            var resultList = new List<ExpiryNotificationDto>();

            foreach (var n in notifications)
            {
                var product = await _unitOfWork.ProductRepository.GetByIdAsync(n.ProductId, cancellationToken);
                var batch = await _unitOfWork.BatchRepository.GetByIdAsync(n.BatchId, cancellationToken);

                // إذا كانت الدفعة قد نفدت بالكامل أثناء حركة بيع، نقوم بإغلاق الإشعار
                if (batch is null || batch.RemainingQuantity <= 0)
                {
                    n.Resolve(null);
                    _unitOfWork.NotificationRepository.Update(n);
                    continue;
                }

                int daysRemaining = batch.ExpiryDate.HasValue ? (int)(batch.ExpiryDate.Value.Date - today).TotalDays : n.DaysRemainingAtCreation;
                bool isExpired = daysRemaining < 0;

                resultList.Add(new ExpiryNotificationDto(
                    n.Id,
                    n.ProductId,
                    product?.NameAr ?? "منتج غير معروف",
                    product?.Barcode ?? "-",
                    n.BatchId,
                    batch.BatchNumber,
                    batch.RemainingQuantity,
                    product?.BaseUnit ?? "قطعة",
                    product?.ParentUnit,
                    product?.ConversionFactor ?? 1,
                    batch.UnitCost,
                    batch.ExpiryDate,
                    daysRemaining,
                    isExpired,
                    n.Message,
                    n.Status,
                    n.CreatedAt));
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<IReadOnlyList<ExpiryNotificationDto>>.Success(resultList);
        }
    }
}
