using Inventory.Domain;
using Inventory.Domain.Stock.StockMovements;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Returns.Domain;
using Returns.Domain.Returns.Entities;

namespace Returns.Application.PurchaseReturns.Commands.CreatePurchaseReturn
{
    internal sealed class CreatePurchaseReturnCommandHandler : ICommandHandler<CreatePurchaseReturnCommand, Guid>
    {
        private readonly IReturnsUnitOfWork _returnsUnitOfWork;
        private readonly IInventoryUnitOfWork _inventoryUnitOfWork;

        public CreatePurchaseReturnCommandHandler(
            IReturnsUnitOfWork returnsUnitOfWork,
            IInventoryUnitOfWork inventoryUnitOfWork)
        {
            _returnsUnitOfWork = returnsUnitOfWork;
            _inventoryUnitOfWork = inventoryUnitOfWork;
        }

        public async Task<Result<Guid>> Handle(CreatePurchaseReturnCommand request, CancellationToken cancellationToken)
        {
            var returnNumber = $"PR-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";

            var returnResult = PurchaseReturn.Create(
                returnNumber, request.OriginalPurchaseId, request.SupplierId,
                request.CreatedByUserId, request.Reason, request.Notes);

            if (returnResult.IsFailure)
                return Result<Guid>.Failure(returnResult.Error);

            var purchaseReturn = returnResult.Value!;

            foreach (var itemReq in request.Items)
            {
                var itemResult = purchaseReturn.AddItem(
                    itemReq.ProductId, itemReq.Quantity, itemReq.UnitCost, itemReq.Tax);

                if (itemResult.IsFailure)
                    return Result<Guid>.Failure(itemResult.Error);
            }

            var completeResult = purchaseReturn.Complete();
            if (completeResult.IsFailure)
                return Result<Guid>.Failure(completeResult.Error);

            await _returnsUnitOfWork.PurchaseReturnRepository.AddAsync(purchaseReturn);

            var purchaseBatches = request.OriginalPurchaseId != Guid.Empty
                ? await _inventoryUnitOfWork.BatchRepository.GetBatchesByPurchaseIdAsync(request.OriginalPurchaseId, cancellationToken)
                : null;

            // نقصان رصيد المخزون وتسجيل حركة المخزون
            foreach (var item in purchaseReturn.Items)
            {
                var product = await _inventoryUnitOfWork.ProductRepository.GetByIdAsync(item.ProductId, cancellationToken);
                if (product is not null)
                {
                    var factor = product.ConversionFactor > 1 ? product.ConversionFactor : 1;
                    var deductPieces = item.Quantity * factor;

                    product.AdjustStock(-deductPieces, allowNegativeStock: true);
                    _inventoryUnitOfWork.ProductRepository.Update(product);

                    // خصم من التشغيلة/الدفعة المرتبطة بفاتورة الشراء إن وجدت
                    if (purchaseBatches is not null)
                    {
                        var batch = purchaseBatches.FirstOrDefault(b => b.ProductId == item.ProductId && b.RemainingQuantity > 0);
                        if (batch is not null)
                        {
                            var qtyToDeductFromBatch = Math.Min(deductPieces, batch.RemainingQuantity);
                            if (qtyToDeductFromBatch > 0)
                            {
                                batch.Deduct(qtyToDeductFromBatch);
                                _inventoryUnitOfWork.BatchRepository.Update(batch);
                            }
                        }
                    }

                    var movementResult = StockMovement.Create(
                        item.ProductId,
                        -deductPieces,
                        StockMovementType.PurchaseReturn,
                        request.CreatedByUserId,
                        reference: purchaseReturn.ReturnNumber,
                        notes: $"مرتجع شراء - رقم {purchaseReturn.ReturnNumber} (الكمية: {item.Quantity} {(factor > 1 ? product.ParentUnit ?? "كرتونة" : product.BaseUnit)} = {deductPieces} قطعة)");

                    if (movementResult.IsSuccess)
                        await _inventoryUnitOfWork.StockMovementRepository.AddAsync(movementResult.Value!);
                }
            }

            await _returnsUnitOfWork.SaveChangesAsync(cancellationToken);
            await _inventoryUnitOfWork.SaveChangesAsync(cancellationToken);

            return Result<Guid>.Success(purchaseReturn.Id);
        }
    }
}
