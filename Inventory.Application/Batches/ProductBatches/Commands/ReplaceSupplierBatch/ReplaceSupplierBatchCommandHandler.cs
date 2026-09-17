using Inventory.Domain;
using Inventory.Domain.Stock.StockMovements;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Inventory.Application.Batches.ProductBatches.Commands.ReplaceSupplierBatch
{
    internal sealed class ReplaceSupplierBatchCommandHandler : ICommandHandler<ReplaceSupplierBatchCommand>
    {
        private readonly IInventoryUnitOfWork _inventoryUnitOfWork;

        public ReplaceSupplierBatchCommandHandler(IInventoryUnitOfWork inventoryUnitOfWork)
        {
            _inventoryUnitOfWork = inventoryUnitOfWork;
        }

        public async Task<Result> Handle(ReplaceSupplierBatchCommand request, CancellationToken cancellationToken)
        {
            var batch = await _inventoryUnitOfWork.BatchRepository.GetByIdAsync(request.BatchId, cancellationToken);
            if (batch is null)
                return Result.Failure(new Error("Batch.NotFound", "دفعة المخزون غير موجودة."));

            var replaceResult = batch.ReplaceBySupplier(request.NewExpiryDate, request.NewBatchNumber);
            if (replaceResult.IsFailure)
                return replaceResult;

            _inventoryUnitOfWork.BatchRepository.Update(batch);

            // If related to an expiry notification, resolve it
            if (request.RelatedNotificationId.HasValue && request.RelatedNotificationId.Value != Guid.Empty)
            {
                var notification = await _inventoryUnitOfWork.NotificationRepository.GetByIdAsync(request.RelatedNotificationId.Value, cancellationToken);
                if (notification != null)
                {
                    notification.Resolve(request.UserId);
                    _inventoryUnitOfWork.NotificationRepository.Update(notification);
                }
            }

            // Record a stock audit movement logging the supplier replacement
            var effectiveUserId = request.UserId.HasValue && request.UserId.Value != Guid.Empty
                ? request.UserId.Value
                : Guid.NewGuid();

            var movementResult = StockMovement.Create(
                batch.ProductId,
                0,
                StockMovementType.Adjustment,
                effectiveUserId,
                reference: batch.BatchNumber,
                notes: string.IsNullOrWhiteSpace(request.Notes)
                    ? $"استبدال من المورد بتاريخ إنتاج/صلاحية جديد: {request.NewExpiryDate:dd/MM/yyyy}"
                    : $"استبدال من المورد بتاريخ جديد ({request.NewExpiryDate:dd/MM/yyyy}) - {request.Notes}");

            if (movementResult.IsSuccess)
            {
                await _inventoryUnitOfWork.StockMovementRepository.AddAsync(movementResult.Value!);
            }

            await _inventoryUnitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}
