using Inventory.Domain;
using Inventory.Domain.Stock.StockMovements;
using POS.Shared.Application.IService;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Returns.Domain;
using Returns.Domain.Returns.Entities;
using Shifts.Domain;

namespace Returns.Application.SalesReturns.Commands.CreateSalesReturn
{
    internal sealed class CreateSalesReturnCommandHandler : ICommandHandler<CreateSalesReturnCommand, Guid>
    {
        private readonly IReturnsUnitOfWork _returnsUnitOfWork;
        private readonly IInventoryUnitOfWork _inventoryUnitOfWork;
        private readonly IShiftsUnitOfWork _shiftsUnitOfWork;
        private readonly ICacheService _cacheService;

        public CreateSalesReturnCommandHandler(
            IReturnsUnitOfWork returnsUnitOfWork,
            IInventoryUnitOfWork inventoryUnitOfWork,
            IShiftsUnitOfWork shiftsUnitOfWork,
            ICacheService cacheService)
        {
            _returnsUnitOfWork = returnsUnitOfWork;
            _inventoryUnitOfWork = inventoryUnitOfWork;
            _shiftsUnitOfWork = shiftsUnitOfWork;
            _cacheService = cacheService;
        }

        public async Task<Result<Guid>> Handle(CreateSalesReturnCommand request, CancellationToken cancellationToken)
        {
            var returnNumber = $"SR-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";

            var returnResult = SalesReturn.Create(
                returnNumber, request.OriginalSaleId, request.CashierId, request.ShiftId,
                request.CustomerId, (RefundMethod)request.RefundMethod, request.Reason, request.Notes);

            if (returnResult.IsFailure)
                return Result<Guid>.Failure(returnResult.Error);

            var salesReturn = returnResult.Value!;

            foreach (var itemReq in request.Items)
            {
                var itemResult = salesReturn.AddItem(
                    itemReq.ProductId, itemReq.OriginalSaleItemId, itemReq.Quantity,
                    itemReq.UnitPrice, itemReq.Tax, itemReq.Reason);

                if (itemResult.IsFailure)
                    return Result<Guid>.Failure(itemResult.Error);
            }

            var completeResult = salesReturn.Complete();
            if (completeResult.IsFailure)
                return Result<Guid>.Failure(completeResult.Error);

            await _returnsUnitOfWork.SalesReturnRepository.AddAsync(salesReturn);

            // زيادة رصيد المخزون وتسجيل حركة المخزون
            foreach (var item in salesReturn.Items)
            {
                var product = await _inventoryUnitOfWork.ProductRepository.GetByIdAsync(item.ProductId, cancellationToken);
                if (product is not null)
                {
                    product.AdjustStock(item.Quantity, allowNegativeStock: true);
                    _inventoryUnitOfWork.ProductRepository.Update(product);

                    var movementResult = StockMovement.Create(
                        item.ProductId,
                        item.Quantity,
                        StockMovementType.SaleReturn,
                        request.CashierId,
                        reference: salesReturn.ReturnNumber,
                        notes: $"مرتجع مبيعات - رقم {salesReturn.ReturnNumber}");

                    if (movementResult.IsSuccess)
                        await _inventoryUnitOfWork.StockMovementRepository.AddAsync(movementResult.Value!);
                }
            }

            // تحديث الشفت (خصم الكاش وتسجيل المرتجع)
            var shift = await _shiftsUnitOfWork.ShiftRepository.GetByIdAsync(request.ShiftId, cancellationToken);
            if (shift is not null)
            {
                shift.RecordReturn(salesReturn.TotalAmount, salesReturn.RefundMethod.ToString());
                _shiftsUnitOfWork.ShiftRepository.Update(shift);
            }

            await _returnsUnitOfWork.SaveChangesAsync(cancellationToken);
            await _inventoryUnitOfWork.SaveChangesAsync(cancellationToken);
            await _shiftsUnitOfWork.SaveChangesAsync(cancellationToken);

            await _cacheService.RemoveByPrefixAsync("dashboard_", cancellationToken);
            await _cacheService.RemoveByPrefixAsync("monthly_calendar_", cancellationToken);

            return Result<Guid>.Success(salesReturn.Id);
        }
    }
}
