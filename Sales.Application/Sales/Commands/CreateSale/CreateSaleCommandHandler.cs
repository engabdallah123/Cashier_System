using Inventory.Domain;
using Inventory.Domain.Stock.StockMovements;
using POS.Shared.Application.IService;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Sales.Domain;
using Sales.Domain.Sales;
using Sales.Domain.Sales.Entities;
using Settings.Domain;
using Shifts.Domain;

namespace Sales.Application.Sales.Commands.CreateSale
{
    internal sealed class CreateSaleCommandHandler : ICommandHandler<CreateSaleCommand, Guid>
    {
        private readonly ISalesUnitOfWork _salesUnitOfWork;
        private readonly IInventoryUnitOfWork _inventoryUnitOfWork;
        private readonly IShiftsUnitOfWork _shiftsUnitOfWork;
        private readonly ISettingsUnitOfWork _settingsUnitOfWork;
        private readonly ICacheService _cacheService;

        public CreateSaleCommandHandler(
            ISalesUnitOfWork salesUnitOfWork,
            IInventoryUnitOfWork inventoryUnitOfWork,
            IShiftsUnitOfWork shiftsUnitOfWork,
            ISettingsUnitOfWork settingsUnitOfWork,
            ICacheService cacheService)
        {
            _salesUnitOfWork = salesUnitOfWork;
            _inventoryUnitOfWork = inventoryUnitOfWork;
            _shiftsUnitOfWork = shiftsUnitOfWork;
            _settingsUnitOfWork = settingsUnitOfWork;
            _cacheService = cacheService;
        }

        public async Task<Result<Guid>> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
        {
            // 1. التحقق من وجود شفت مفتوح للكاشير
            var shift = await _shiftsUnitOfWork.ShiftRepository.GetByIdAsync(request.ShiftId, cancellationToken);
            if (shift is null || shift.CashierId != request.CashierId || shift.Status != Shifts.Domain.Shifts.Entities.ShiftStatus.Open)
                return Result<Guid>.Failure(SaleErrors.NoOpenShiftAvailable);

            // 2. فحص إعدادات التنسيق وحظر المخزون السالب
            var settings = (await _settingsUnitOfWork.StoreSettingRepository.GetAllAsync()).FirstOrDefault();
            var allowNegativeStock = settings?.AllowNegativeStock ?? false;

            // 3. التحقق من توفر رصيد المخزون لكل عنصر
            foreach (var itemReq in request.Items)
            {
                var product = await _inventoryUnitOfWork.ProductRepository.GetByIdAsync(itemReq.ProductId, cancellationToken);
                if (product is null)
                    return Result<Guid>.Failure(new Error("Product.NotFound", $"المنتج '{itemReq.ProductId}' غير موجود."));

                if (!allowNegativeStock && product.QuantityInStock < itemReq.Quantity)
                    return Result<Guid>.Failure(new Error("Stock.Insufficient", $"الكمية المتاحة من '{product.NameAr}' هي {product.QuantityInStock} فقط."));
            }

            // 4. إنشاء الفاتورة
            int orderNumber = shift.TotalInvoices + 1;
            var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}-{orderNumber:D3}";

            var saleResult = Sale.Create(
                invoiceNumber, request.CashierId, request.ShiftId,
                request.CustomerId, request.DiscountAmount, request.TaxAmount,
                request.PaidAmount, request.PaymentMethod, request.Notes);

            if (saleResult.IsFailure)
                return Result<Guid>.Failure(saleResult.Error);

            var sale = saleResult.Value!;

            foreach (var itemReq in request.Items)
            {
                var itemResult = sale.AddItem(itemReq.ProductId, itemReq.Quantity, itemReq.UnitPrice, itemReq.Discount, itemReq.Tax);
                if (itemResult.IsFailure)
                    return Result<Guid>.Failure(itemResult.Error);
            }

            var completeResult = sale.Complete();
            if (completeResult.IsFailure)
                return Result<Guid>.Failure(completeResult.Error);

            await _salesUnitOfWork.SaleRepository.AddAsync(sale);

            // 5. خصم الكمية من المخزون وتسجيل حركة المخزون وخصم دفعات المخزون بنظام FIFO
            foreach (var item in sale.Items)
            {
                var product = await _inventoryUnitOfWork.ProductRepository.GetByIdAsync(item.ProductId, cancellationToken);
                if (product is not null)
                {
                    product.AdjustStock(-item.Quantity, allowNegativeStock);
                    _inventoryUnitOfWork.ProductRepository.Update(product);

                    // خصم من الدفعات بنظام FIFO الصارم (PurchaseDate ثم CreatedAt)
                    var activeBatches = await _inventoryUnitOfWork.BatchRepository.GetActiveBatchesByProductIdAsync(item.ProductId, cancellationToken);
                    decimal remainingToDeduct = item.Quantity;

                    foreach (var batch in activeBatches)
                    {
                        if (remainingToDeduct <= 0) break;

                        decimal deductAmount = Math.Min(batch.RemainingQuantity, remainingToDeduct);
                        var deductResult = batch.Deduct(deductAmount);
                        if (deductResult.IsSuccess)
                        {
                            _inventoryUnitOfWork.BatchRepository.Update(batch);
                            remainingToDeduct -= deductAmount;
                        }
                    }

                    var movementResult = StockMovement.Create(
                        item.ProductId,
                        -item.Quantity,
                        StockMovementType.Sale,
                        request.CashierId,
                        reference: sale.InvoiceNumber,
                        notes: $"مبيعات فاتورة {sale.InvoiceNumber}");

                    if (movementResult.IsSuccess)
                        await _inventoryUnitOfWork.StockMovementRepository.AddAsync(movementResult.Value!);
                }
            }

            // 6. تحديث إجماليات الشفت
            shift.RecordSale(sale.TotalAmount, sale.DiscountAmount, sale.TaxAmount, sale.PaymentMethod, sale.PaidAmount);
            _shiftsUnitOfWork.ShiftRepository.Update(shift);

            // 7. حفظ التغييرات في كل الوجهات
            await _salesUnitOfWork.SaveChangesAsync(cancellationToken);
            await _inventoryUnitOfWork.SaveChangesAsync(cancellationToken);
            await _shiftsUnitOfWork.SaveChangesAsync(cancellationToken);

            // 8. مسح كاش لوحة التحكم والتقارير الشهرية لضمان تحديث الأرقام فورياً
            await _cacheService.RemoveByPrefixAsync("dashboard_", cancellationToken);
            await _cacheService.RemoveByPrefixAsync("monthly_calendar_", cancellationToken);

            return Result<Guid>.Success(sale.Id);
        }
    }
}
