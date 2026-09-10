using Inventory.Domain;
using Inventory.Domain.Stock.StockMovements;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Returns.Domain;
using Returns.Domain.Returns;
using Shifts.Domain;

namespace Returns.Application.SalesReturns.Commands.DeleteSalesReturn
{
    internal sealed class DeleteSalesReturnCommandHandler : ICommandHandler<DeleteSalesReturnCommand>
    {
        private readonly IReturnsUnitOfWork _returnsUnitOfWork;
        private readonly IInventoryUnitOfWork _inventoryUnitOfWork;
        private readonly IShiftsUnitOfWork _shiftsUnitOfWork;

        public DeleteSalesReturnCommandHandler(
            IReturnsUnitOfWork returnsUnitOfWork,
            IInventoryUnitOfWork inventoryUnitOfWork,
            IShiftsUnitOfWork shiftsUnitOfWork)
        {
            _returnsUnitOfWork = returnsUnitOfWork;
            _inventoryUnitOfWork = inventoryUnitOfWork;
            _shiftsUnitOfWork = shiftsUnitOfWork;
        }

        public async Task<Result> Handle(DeleteSalesReturnCommand request, CancellationToken cancellationToken)
        {
            var salesReturn = await _returnsUnitOfWork.SalesReturnRepository.FindAsync(
                sr => sr.Id == request.Id,
                new[] { "Items" });

            if (salesReturn is null)
                return Result.Failure(ReturnErrors.NotFound(request.Id));

            // 1. Revert stock: decrease stock by the returned quantity and record stock movement
            foreach (var item in salesReturn.Items)
            {
                var product = await _inventoryUnitOfWork.ProductRepository.GetByIdAsync(item.ProductId, cancellationToken);
                if (product is not null)
                {
                    product.AdjustStock(-item.Quantity, allowNegativeStock: true);
                    _inventoryUnitOfWork.ProductRepository.Update(product);

                    var movementResult = StockMovement.Create(
                        item.ProductId,
                        -item.Quantity,
                        StockMovementType.Adjustment,
                        request.UserId,
                        reference: salesReturn.ReturnNumber,
                        notes: $"حذف مرتجع مبيعات - رقم {salesReturn.ReturnNumber}");

                    if (movementResult.IsSuccess)
                    {
                        await _inventoryUnitOfWork.StockMovementRepository.AddAsync(movementResult.Value!);
                    }
                }
            }

            // 2. Adjust shift: deduct the recorded return amount
            var shift = await _shiftsUnitOfWork.ShiftRepository.GetByIdAsync(salesReturn.ShiftId, cancellationToken);
            if (shift is not null)
            {
                shift.RecordReturn(-salesReturn.TotalAmount, salesReturn.RefundMethod.ToString());
                _shiftsUnitOfWork.ShiftRepository.Update(shift);
            }

            // 3. Delete items & return entity
            var itemsList = salesReturn.Items.ToList();
            if (itemsList.Any())
            {
                _returnsUnitOfWork.SalesReturnItemRepository.DeleteRange(itemsList);
            }

            _returnsUnitOfWork.SalesReturnRepository.Delete(salesReturn);

            await _returnsUnitOfWork.SaveChangesAsync(cancellationToken);
            await _inventoryUnitOfWork.SaveChangesAsync(cancellationToken);
            await _shiftsUnitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
