using Inventory.Domain;
using Inventory.Domain.Stock.StockMovements;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Purchases.Domain;
using Purchases.Domain.Purchases;
using Purchases.Domain.Purchases.Entities;

namespace Purchases.Application.Purchases.Commands.UpdatePurchase
{
    internal sealed class UpdatePurchaseCommandHandler : ICommandHandler<UpdatePurchaseCommand>
    {
        private readonly IPurchasesUnitOfWork _purchasesUnitOfWork;
        private readonly IInventoryUnitOfWork _inventoryUnitOfWork;

        public UpdatePurchaseCommandHandler(
            IPurchasesUnitOfWork purchasesUnitOfWork,
            IInventoryUnitOfWork inventoryUnitOfWork)
        {
            _purchasesUnitOfWork = purchasesUnitOfWork;
            _inventoryUnitOfWork = inventoryUnitOfWork;
        }

        public async Task<Result> Handle(UpdatePurchaseCommand request, CancellationToken cancellationToken)
        {
            var purchase = await _purchasesUnitOfWork.PurchaseRepository.FindAsync(
                p => p.Id == request.Id,
                new[] { "Items" });

            if (purchase is null)
                return Result.Failure(PurchaseErrors.NotFound(request.Id));

            if (!request.Items.Any())
                return Result.Failure(PurchaseErrors.PurchaseHasNoItems);

            // إذا كانت الفاتورة مستلمة، نقوم أولاً بخصم الكميات القديمة وحذف دفعات المخزون المرتبطة بها
            if (purchase.Status == PurchaseStatus.Received)
            {
                var oldBatches = await _inventoryUnitOfWork.BatchRepository.GetBatchesByPurchaseIdAsync(purchase.Id, cancellationToken);
                foreach (var oldItem in purchase.Items)
                {
                    var product = await _inventoryUnitOfWork.ProductRepository.GetByIdAsync(oldItem.ProductId, cancellationToken);
                    if (product is not null)
                    {
                        var batch = oldBatches.FirstOrDefault(b => b.PurchaseInvoiceItemId == oldItem.Id);
                        decimal qtyToDeduct = batch?.BaseQuantity ?? oldItem.Quantity;

                        product.AdjustStock(-qtyToDeduct, allowNegativeStock: true);
                        _inventoryUnitOfWork.ProductRepository.Update(product);

                        if (batch is not null)
                        {
                            _inventoryUnitOfWork.BatchRepository.Delete(batch);
                        }
                    }
                }
            }

            // حذف العناصر القديمة من الـ Repository
            var oldItemsList = purchase.Items.ToList();
            if (oldItemsList.Any())
            {
                _purchasesUnitOfWork.PurchaseItemRepository.DeleteRange(oldItemsList);
            }
            purchase.ClearItems();

            // تحديث البيانات الأساسية للفاتورة
            var updateDetailsResult = purchase.UpdateDetails(
                request.InvoiceNumber, request.SupplierId, request.InternalNumber,
                request.DiscountAmount, request.TaxAmount, request.PaidAmount,
                (PaymentMethod)request.PaymentMethod, request.Notes,
                request.PurchaseDate);

            if (updateDetailsResult.IsFailure)
                return updateDetailsResult;

            int todayBatchSeq = 0;
            if (purchase.Status == PurchaseStatus.Received)
            {
                todayBatchSeq = await _inventoryUnitOfWork.BatchRepository.GetMaxTodayBatchSequenceAsync(purchase.PurchaseDate, cancellationToken);
            }

            // إضافة الأصناف الجديدة وضبط المخزون والدفعات
            foreach (var itemReq in request.Items)
            {
                var addResult = purchase.AddItemDirect(
                    itemReq.ProductId, itemReq.Quantity, itemReq.UnitCost,
                    itemReq.Discount, itemReq.Tax, itemReq.ExpiryDate, itemReq.BatchNumber);

                if (addResult.IsFailure)
                    return addResult;

                var newItem = addResult.Value!;
                await _purchasesUnitOfWork.PurchaseItemRepository.AddAsync(newItem);

                if (purchase.Status == PurchaseStatus.Received)
                {
                    var product = await _inventoryUnitOfWork.ProductRepository.GetByIdAsync(itemReq.ProductId, cancellationToken);
                    if (product is not null)
                    {
                        bool isParentUnit = !string.IsNullOrWhiteSpace(itemReq.Unit) &&
                                            (string.Equals(itemReq.Unit.Trim(), "كرتونة", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(itemReq.Unit.Trim(), "Carton", StringComparison.OrdinalIgnoreCase) ||
                                             (!string.IsNullOrWhiteSpace(product.ParentUnit) && string.Equals(itemReq.Unit.Trim(), product.ParentUnit.Trim(), StringComparison.OrdinalIgnoreCase)));

                        int factor = (isParentUnit && product.ConversionFactor > 1) ? product.ConversionFactor : 1;
                        decimal baseQuantity = itemReq.Quantity * factor;
                        decimal unitCostPerPiece = factor > 1 ? Math.Round(itemReq.UnitCost / factor, 4) : itemReq.UnitCost;

                        product.AdjustStock(baseQuantity, allowNegativeStock: true);
                        _inventoryUnitOfWork.ProductRepository.Update(product);

                        if (product.TrackExpiry)
                        {
                            todayBatchSeq++;
                            var batchNumber = $"BATCH-{purchase.PurchaseDate:yyyyMMdd}-{todayBatchSeq}";
                            newItem.UpdateBatchNumber(batchNumber);

                            var expiryDate = itemReq.ExpiryDate;
                            if (!expiryDate.HasValue && product.ShelfLifeDays > 0)
                            {
                                expiryDate = purchase.PurchaseDate.AddDays(product.ShelfLifeDays);
                            }

                            var batchResult = Inventory.Domain.Batches.Entities.InventoryBatch.Create(
                                productId: product.Id,
                                purchaseInvoiceId: purchase.Id,
                                purchaseInvoiceItemId: newItem.Id,
                                batchNumber: batchNumber,
                                originalQuantity: itemReq.Quantity,
                                originalUnit: !string.IsNullOrWhiteSpace(itemReq.Unit) ? itemReq.Unit : (factor > 1 ? (product.ParentUnit ?? "كرتونة") : product.BaseUnit),
                                baseQuantity: baseQuantity,
                                unitCost: unitCostPerPiece,
                                purchaseDate: purchase.PurchaseDate,
                                expiryDate: expiryDate);

                            if (batchResult.IsSuccess)
                            {
                                await _inventoryUnitOfWork.BatchRepository.AddAsync(batchResult.Value!, cancellationToken);
                            }
                        }
                        else
                        {
                            newItem.UpdateBatchNumber(null);
                        }

                        var movementResult = StockMovement.Create(
                            itemReq.ProductId,
                            baseQuantity,
                            StockMovementType.Purchase,
                            request.UserId,
                            reference: purchase.InvoiceNumber,
                            notes: $"تعديل فاتورة شراء - رقم {purchase.InvoiceNumber} (الوحدة: {(factor > 1 ? product.ParentUnit ?? "كرتونة" : product.BaseUnit)})");

                        if (movementResult.IsSuccess)
                        {
                            await _inventoryUnitOfWork.StockMovementRepository.AddAsync(movementResult.Value!);
                        }
                    }
                }
            }

            await _purchasesUnitOfWork.SaveChangesAsync(cancellationToken);
            await _inventoryUnitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
