using Inventory.Domain;
using Inventory.Domain.Stock.StockMovements;
using Inventory.Domain.Stock.Waste.Entities;
using POS.Shared.Application.IService;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Inventory.Application.Stock.Waste.Commands.RecordWaste
{
    internal sealed class RecordWasteCommandHandler : ICommandHandler<RecordWasteCommand, Guid>
    {
        private readonly IInventoryUnitOfWork _unitOfWork;
        private readonly ICacheService _cacheService;

        public RecordWasteCommandHandler(
            IInventoryUnitOfWork unitOfWork,
            ICacheService cacheService)
        {
            _unitOfWork = unitOfWork;
            _cacheService = cacheService;
        }

        public async Task<Result<Guid>> Handle(RecordWasteCommand request, CancellationToken cancellationToken)
        {
            var product = await _unitOfWork.ProductRepository.GetByIdAsync(request.ProductId, cancellationToken);
            if (product is null)
                return Result<Guid>.Failure(new Error("Product.NotFound", "المنتج المحدد غير موجود."));

            var batch = await _unitOfWork.BatchRepository.GetByIdAsync(request.InventoryBatchId, cancellationToken);
            if (batch is null)
                return Result<Guid>.Failure(new Error("Batch.NotFound", "الدفعة المحددة غير موجودة."));

            if (batch.ProductId != product.Id)
                return Result<Guid>.Failure(new Error("Batch.Mismatch", "الدفعة المحددة لا تنتمي لهذا المنتج."));

            bool isParentUnit = !string.IsNullOrWhiteSpace(request.Unit) &&
                                (string.Equals(request.Unit.Trim(), "كرتونة", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(request.Unit.Trim(), "Carton", StringComparison.OrdinalIgnoreCase) ||
                                 (!string.IsNullOrWhiteSpace(product.ParentUnit) && string.Equals(request.Unit.Trim(), product.ParentUnit.Trim(), StringComparison.OrdinalIgnoreCase)));

            int factor = (isParentUnit && product.ConversionFactor > 1) ? product.ConversionFactor : 1;
            decimal baseQty = request.Quantity * factor;

            if (baseQty <= 0)
                return Result<Guid>.Failure(new Error("Waste.InvalidQuantity", "كمية الهالك يجب أن تكون أكبر من الصفر."));

            if (baseQty > batch.RemainingQuantity)
                return Result<Guid>.Failure(new Error("Waste.ExceedsBatch", $"كمية الهالك ({baseQty} قطعة) تتجاوز الكمية المتبقية في الدفعة ({batch.RemainingQuantity} قطعة)."));

            decimal unitCost = batch.UnitCost;
            decimal totalCost = Math.Round(baseQty * unitCost, 2);

            // 1. خصم الكمية من الدفعة
            var deductResult = batch.Deduct(baseQty);
            if (deductResult.IsFailure)
                return Result<Guid>.Failure(deductResult.Error);

            _unitOfWork.BatchRepository.Update(batch);

            // 2. خصم الكمية من رصيد المنتج الإجمالي
            var adjustStockResult = product.AdjustStock(-baseQty, allowNegativeStock: false);
            if (adjustStockResult.IsFailure)
                return Result<Guid>.Failure(adjustStockResult.Error);

            _unitOfWork.ProductRepository.Update(product);

            // 3. إنشاء سجل الهالك
            var wasteResult = InventoryWaste.Create(
                productId: request.ProductId,
                inventoryBatchId: request.InventoryBatchId,
                quantity: request.Quantity,
                unit: request.Unit,
                baseQuantity: baseQty,
                unitCost: unitCost,
                reason: request.Reason,
                source: request.Source,
                createdBy: request.CreatedBy,
                purchaseInvoiceId: batch.PurchaseInvoiceId,
                purchaseInvoiceItemId: batch.PurchaseInvoiceItemId,
                relatedNotificationId: request.RelatedNotificationId,
                notes: request.Notes);

            if (wasteResult.IsFailure)
                return Result<Guid>.Failure(wasteResult.Error);

            var waste = wasteResult.Value!;
            await _unitOfWork.WasteRepository.AddAsync(waste, cancellationToken);

            // 4. إذا كان الهالك مرتبطاً بإشعار صلاحية، نقوم بإغلاق الإشعار وربطه بالهالك
            if (request.RelatedNotificationId.HasValue)
            {
                var notification = await _unitOfWork.NotificationRepository.GetByIdAsync(request.RelatedNotificationId.Value, cancellationToken);
                if (notification is not null)
                {
                    notification.LinkWaste(waste.Id);
                    _unitOfWork.NotificationRepository.Update(notification);
                }
            }

            // 5. تسجيل حركة مخزون هالك
            var movementResult = StockMovement.Create(
                request.ProductId,
                -baseQty,
                StockMovementType.Waste,
                request.CreatedBy,
                reference: batch.BatchNumber,
                notes: $"تسجيل هالك: {request.Reason} (تكلفة: {totalCost:N2} ج.م) - {request.Notes}");

            if (movementResult.IsSuccess)
            {
                await _unitOfWork.StockMovementRepository.AddAsync(movementResult.Value!);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _cacheService.RemoveByPrefixAsync("dashboard_", cancellationToken);

            return Result<Guid>.Success(waste.Id);
        }
    }
}
