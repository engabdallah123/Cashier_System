using Inventory.Domain;
using Inventory.Domain.Stock.StockMovements;
using POS.Shared.Application.IService;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Purchases.Domain;
using Purchases.Domain.Purchases.Entities;

namespace Purchases.Application.Purchases.Commands.CreatePurchase
{
    internal sealed class CreatePurchaseCommandHandler : ICommandHandler<CreatePurchaseCommand, Guid>
    {
        private readonly IPurchasesUnitOfWork _purchasesUnitOfWork;
        private readonly IInventoryUnitOfWork _inventoryUnitOfWork;
        private readonly ICacheService _cacheService;

        public CreatePurchaseCommandHandler(
            IPurchasesUnitOfWork purchasesUnitOfWork,
            IInventoryUnitOfWork inventoryUnitOfWork,
            ICacheService cacheService)
        {
            _purchasesUnitOfWork = purchasesUnitOfWork;
            _inventoryUnitOfWork = inventoryUnitOfWork;
            _cacheService = cacheService;
        }

        public async Task<Result<Guid>> Handle(CreatePurchaseCommand request, CancellationToken cancellationToken)
        {
            var purchaseResult = Purchase.Create(
                request.InvoiceNumber, request.SupplierId, request.CreatedByUserId,
                request.InternalNumber, request.DiscountAmount, request.TaxAmount,
                request.PaidAmount, (PaymentMethod)request.PaymentMethod, request.Notes,
                request.PurchaseDate);

            if (purchaseResult.IsFailure)
                return Result<Guid>.Failure(purchaseResult.Error);

            var purchase = purchaseResult.Value!;

            foreach (var itemReq in request.Items)
            {
                var itemResult = purchase.AddItem(
                    itemReq.ProductId, itemReq.Quantity, itemReq.UnitCost,
                    itemReq.Discount, itemReq.Tax, itemReq.ExpiryDate, itemReq.BatchNumber);

                if (itemResult.IsFailure)
                    return Result<Guid>.Failure(itemResult.Error);
            }

            var receiveResult = purchase.Receive();
            if (receiveResult.IsFailure)
                return Result<Guid>.Failure(receiveResult.Error);

            await _purchasesUnitOfWork.PurchaseRepository.AddAsync(purchase);

            int todayBatchSeq = await _inventoryUnitOfWork.BatchRepository.GetMaxTodayBatchSequenceAsync(purchase.PurchaseDate, cancellationToken);

            // زيادة رصيد المخزون وتسجيل حركة المخزون وإنشاء دفعات المخزون (InventoryBatches)
            for (int i = 0; i < purchase.Items.Count; i++)
            {
                var item = purchase.Items[i];
                var itemReq = request.Items[i];

                var product = await _inventoryUnitOfWork.ProductRepository.GetByIdAsync(item.ProductId, cancellationToken);
                if (product is not null)
                {
                    // التحقق مما إذا كانت الوحدة المدخلة هي وحدة كرتونة / التجزئة الكبرى
                    bool isParentUnit = !string.IsNullOrWhiteSpace(itemReq.Unit) &&
                                        (string.Equals(itemReq.Unit.Trim(), "كرتونة", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(itemReq.Unit.Trim(), "Carton", StringComparison.OrdinalIgnoreCase) ||
                                         (!string.IsNullOrWhiteSpace(product.ParentUnit) && string.Equals(itemReq.Unit.Trim(), product.ParentUnit.Trim(), StringComparison.OrdinalIgnoreCase)));

                    int factor = (isParentUnit && product.ConversionFactor > 1) ? product.ConversionFactor : 1;

                    decimal baseQuantity = item.Quantity * factor;
                    decimal unitCostPerPiece = factor > 1 ? Math.Round(item.UnitCost / factor, 4) : item.UnitCost;

                    // زيادة رصيد المنتج بالوحدة الصغرى (القطع) وتحديث سعر التكلفة
                    product.AdjustStock(baseQuantity, allowNegativeStock: true);
                    product.ApplyPrices(unitCostPerPiece, product.SellingPrice, product.WholesalePrice);
                    _inventoryUnitOfWork.ProductRepository.Update(product);

                    // تتبع الصلاحية والدفعات يتم فقط في حال تفعيل تتبع الصلاحية للمنتج
                    if (product.TrackExpiry)
                    {
                        todayBatchSeq++;
                        var batchNumber = $"BATCH-{purchase.PurchaseDate:yyyyMMdd}-{todayBatchSeq}";
                        item.UpdateBatchNumber(batchNumber);

                        // حساب تاريخ الصلاحية المقترح إذا لم يُدخل
                        var expiryDate = item.ExpiryDate;
                        if (!expiryDate.HasValue && product.ShelfLifeDays > 0)
                        {
                            expiryDate = purchase.PurchaseDate.AddDays(product.ShelfLifeDays);
                        }

                        var batchResult = Inventory.Domain.Batches.Entities.InventoryBatch.Create(
                            productId: product.Id,
                            purchaseInvoiceId: purchase.Id,
                            purchaseInvoiceItemId: item.Id,
                            batchNumber: batchNumber,
                            originalQuantity: item.Quantity,
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
                        item.UpdateBatchNumber(null);
                    }

                    var movementResult = StockMovement.Create(
                        item.ProductId,
                        baseQuantity,
                        StockMovementType.Purchase,
                        request.CreatedByUserId,
                        reference: purchase.InvoiceNumber,
                        notes: $"فاتورة شراء - رقم {purchase.InvoiceNumber} (الوحدة: {(factor > 1 ? product.ParentUnit ?? "كرتونة" : product.BaseUnit)})");

                    if (movementResult.IsSuccess)
                    {
                        await _inventoryUnitOfWork.StockMovementRepository.AddAsync(movementResult.Value!);
                    }
                }
            }

            await _purchasesUnitOfWork.SaveChangesAsync(cancellationToken);
            await _inventoryUnitOfWork.SaveChangesAsync(cancellationToken);

            await _cacheService.RemoveByPrefixAsync("dashboard_", cancellationToken);

            return Result<Guid>.Success(purchase.Id);
        }
    }
}
