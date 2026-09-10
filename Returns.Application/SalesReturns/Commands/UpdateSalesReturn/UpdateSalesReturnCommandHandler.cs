using Inventory.Domain;
using Inventory.Domain.Stock.StockMovements;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Returns.Domain;
using Returns.Domain.Returns;
using Returns.Domain.Returns.Entities;
using Shifts.Domain;

namespace Returns.Application.SalesReturns.Commands.UpdateSalesReturn
{
    internal sealed class UpdateSalesReturnCommandHandler : ICommandHandler<UpdateSalesReturnCommand>
    {
        private readonly IReturnsUnitOfWork _returnsUnitOfWork;
        private readonly IInventoryUnitOfWork _inventoryUnitOfWork;
        private readonly IShiftsUnitOfWork _shiftsUnitOfWork;

        public UpdateSalesReturnCommandHandler(
            IReturnsUnitOfWork returnsUnitOfWork,
            IInventoryUnitOfWork inventoryUnitOfWork,
            IShiftsUnitOfWork shiftsUnitOfWork)
        {
            _returnsUnitOfWork = returnsUnitOfWork;
            _inventoryUnitOfWork = inventoryUnitOfWork;
            _shiftsUnitOfWork = shiftsUnitOfWork;
        }

        public async Task<Result> Handle(UpdateSalesReturnCommand request, CancellationToken cancellationToken)
        {
            var salesReturn = await _returnsUnitOfWork.SalesReturnRepository.FindAsync(
                sr => sr.Id == request.Id,
                new[] { "Items" });

            if (salesReturn is null)
                return Result.Failure(ReturnErrors.NotFound(request.Id));

            if (!request.Items.Any())
                return Result.Failure(ReturnErrors.ReturnHasNoItems);

            var oldTotalAmount = salesReturn.TotalAmount;

            // 1. Revert old stock additions (decrease stock by previous return quantities)
            foreach (var oldItem in salesReturn.Items)
            {
                var product = await _inventoryUnitOfWork.ProductRepository.GetByIdAsync(oldItem.ProductId, cancellationToken);
                if (product is not null)
                {
                    product.AdjustStock(-oldItem.Quantity, allowNegativeStock: true);
                    _inventoryUnitOfWork.ProductRepository.Update(product);
                }
            }

            // 2. Delete old return items
            var oldItemsList = salesReturn.Items.ToList();
            if (oldItemsList.Any())
            {
                _returnsUnitOfWork.SalesReturnItemRepository.DeleteRange(oldItemsList);
            }
            salesReturn.ClearItems();

            // 3. Update return header details
            var updateDetailsResult = salesReturn.UpdateDetails(
                (RefundMethod)request.RefundMethod,
                request.Reason,
                request.Notes);

            if (updateDetailsResult.IsFailure)
                return updateDetailsResult;

            // 4. Add new items & adjust stock
            foreach (var itemReq in request.Items)
            {
                var addResult = salesReturn.AddItem(
                    itemReq.ProductId,
                    itemReq.OriginalSaleItemId,
                    itemReq.Quantity,
                    itemReq.UnitPrice,
                    itemReq.Tax,
                    itemReq.Reason);

                if (addResult.IsFailure)
                    return addResult;

                await _returnsUnitOfWork.SalesReturnItemRepository.AddAsync(addResult.Value!);

                var product = await _inventoryUnitOfWork.ProductRepository.GetByIdAsync(itemReq.ProductId, cancellationToken);
                if (product is not null)
                {
                    product.AdjustStock(itemReq.Quantity, allowNegativeStock: true);
                    _inventoryUnitOfWork.ProductRepository.Update(product);

                    var movementResult = StockMovement.Create(
                        itemReq.ProductId,
                        itemReq.Quantity,
                        StockMovementType.SaleReturn,
                        request.UserId,
                        reference: salesReturn.ReturnNumber,
                        notes: $"تعديل مرتجع مبيعات - رقم {salesReturn.ReturnNumber}");

                    if (movementResult.IsSuccess)
                    {
                        await _inventoryUnitOfWork.StockMovementRepository.AddAsync(movementResult.Value!);
                    }
                }
            }

            // 5. Adjust shift if shift is present
            var shift = await _shiftsUnitOfWork.ShiftRepository.GetByIdAsync(salesReturn.ShiftId, cancellationToken);
            if (shift is not null)
            {
                var diff = salesReturn.TotalAmount - oldTotalAmount;
                if (diff != 0)
                {
                    shift.RecordReturn(diff, salesReturn.RefundMethod.ToString());
                    _shiftsUnitOfWork.ShiftRepository.Update(shift);
                }
            }

            await _returnsUnitOfWork.SaveChangesAsync(cancellationToken);
            await _inventoryUnitOfWork.SaveChangesAsync(cancellationToken);
            await _shiftsUnitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
