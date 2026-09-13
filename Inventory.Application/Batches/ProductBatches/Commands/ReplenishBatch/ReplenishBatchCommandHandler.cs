using Inventory.Domain;
using Inventory.Domain.Stock.StockMovements;
using POS.Shared.Application.IService;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Inventory.Application.Batches.ProductBatches.Commands.ReplenishBatch
{
    internal sealed class ReplenishBatchCommandHandler : ICommandHandler<ReplenishBatchCommand>
    {
        private readonly IInventoryUnitOfWork _inventoryUnitOfWork;
        private readonly ICacheService _cacheService;

        public ReplenishBatchCommandHandler(
            IInventoryUnitOfWork inventoryUnitOfWork,
            ICacheService cacheService)
        {
            _inventoryUnitOfWork = inventoryUnitOfWork;
            _cacheService = cacheService;
        }

        public async Task<Result> Handle(ReplenishBatchCommand request, CancellationToken cancellationToken)
        {
            if (request.Quantity <= 0)
                return Result.Failure(new Error("Batch.InvalidQuantity", "الكمية المتبقية للتجديد يجب أن تكون أكبر من الصفر."));

            var batch = await _inventoryUnitOfWork.BatchRepository.GetByIdAsync(request.BatchId, cancellationToken);
            if (batch is null)
                return Result.Failure(new Error("Batch.NotFound", "دفعة المخزون غير موجودة."));

            var product = await _inventoryUnitOfWork.ProductRepository.GetByIdAsync(batch.ProductId, cancellationToken);
            if (product is null)
                return Result.Failure(new Error("Product.NotFound", "المنتج المرتبط بالدفعة غير موجود."));

            // حساب الفرق بين الرصيد الجديد ورصيد الدفعة الحالي لإضافته إلى رصيد المنتج الكلي
            decimal delta = request.Quantity - batch.RemainingQuantity;

            var replenishResult = batch.Replenish(request.Quantity);
            if (replenishResult.IsFailure)
                return replenishResult;

            if (delta != 0)
            {
                product.AdjustStock(delta, allowNegativeStock: true);
                _inventoryUnitOfWork.ProductRepository.Update(product);

                var effectiveUserId = request.UserId.HasValue && request.UserId.Value != Guid.Empty
                    ? request.UserId.Value
                    : Guid.NewGuid();

                var movementResult = StockMovement.Create(
                    batch.ProductId,
                    delta,
                    StockMovementType.Adjustment,
                    effectiveUserId,
                    reference: batch.BatchNumber,
                    notes: string.IsNullOrWhiteSpace(request.Notes)
                        ? $"تجديد رصيد الدفعة {batch.BatchNumber} بالكمية المتبقية فعلياً ({request.Quantity} {batch.OriginalUnit}) بعد جرد المخزون"
                        : request.Notes);

                if (movementResult.IsSuccess)
                {
                    await _inventoryUnitOfWork.StockMovementRepository.AddAsync(movementResult.Value!);
                }
            }

            _inventoryUnitOfWork.BatchRepository.Update(batch);

            await _inventoryUnitOfWork.SaveChangesAsync(cancellationToken);
            await _cacheService.RemoveByPrefixAsync("dashboard_", cancellationToken);

            return Result.Success();
        }
    }
}
