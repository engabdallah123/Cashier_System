using Inventory.Domain;
using Inventory.Domain.Stock.StockMovements;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Returns.Domain;
using Returns.Domain.Returns;
using Returns.Domain.Returns.Entities;

namespace Returns.Application.PurchaseReturns.Commands.UpdatePurchaseReturn
{
    internal sealed class UpdatePurchaseReturnCommandHandler : ICommandHandler<UpdatePurchaseReturnCommand>
    {
        private readonly IReturnsUnitOfWork _returnsUnitOfWork;
        private readonly IInventoryUnitOfWork _inventoryUnitOfWork;

        public UpdatePurchaseReturnCommandHandler(
            IReturnsUnitOfWork returnsUnitOfWork,
            IInventoryUnitOfWork inventoryUnitOfWork)
        {
            _returnsUnitOfWork = returnsUnitOfWork;
            _inventoryUnitOfWork = inventoryUnitOfWork;
        }

        public async Task<Result> Handle(UpdatePurchaseReturnCommand request, CancellationToken cancellationToken)
        {
            var purchaseReturn = await _returnsUnitOfWork.PurchaseReturnRepository.FindAsync(
                pr => pr.Id == request.Id,
                new[] { "Items" });

            if (purchaseReturn is null)
                return Result.Failure(ReturnErrors.NotFound(request.Id));

            if (!request.Items.Any())
                return Result.Failure(ReturnErrors.ReturnHasNoItems);

            // 1. Revert old stock deductions (increase stock by previous return quantities)
            foreach (var oldItem in purchaseReturn.Items)
            {
                var product = await _inventoryUnitOfWork.ProductRepository.GetByIdAsync(oldItem.ProductId, cancellationToken);
                if (product is not null)
                {
                    product.AdjustStock(oldItem.Quantity, allowNegativeStock: true);
                    _inventoryUnitOfWork.ProductRepository.Update(product);
                }
            }

            // 2. Delete old return items
            var oldItemsList = purchaseReturn.Items.ToList();
            if (oldItemsList.Any())
            {
                _returnsUnitOfWork.PurchaseReturnItemRepository.DeleteRange(oldItemsList);
            }
            purchaseReturn.ClearItems();

            // 3. Update return header details
            var updateDetailsResult = purchaseReturn.UpdateDetails(
                request.Reason,
                request.Notes);

            if (updateDetailsResult.IsFailure)
                return updateDetailsResult;

            // 4. Add new items & adjust stock
            foreach (var itemReq in request.Items)
            {
                var addResult = purchaseReturn.AddItem(
                    itemReq.ProductId,
                    itemReq.Quantity,
                    itemReq.UnitCost,
                    itemReq.Tax);

                if (addResult.IsFailure)
                    return addResult;

                await _returnsUnitOfWork.PurchaseReturnItemRepository.AddAsync(addResult.Value!);

                var product = await _inventoryUnitOfWork.ProductRepository.GetByIdAsync(itemReq.ProductId, cancellationToken);
                if (product is not null)
                {
                    product.AdjustStock(-itemReq.Quantity, allowNegativeStock: true);
                    _inventoryUnitOfWork.ProductRepository.Update(product);

                    var movementResult = StockMovement.Create(
                        itemReq.ProductId,
                        -itemReq.Quantity,
                        StockMovementType.PurchaseReturn,
                        request.UserId,
                        reference: purchaseReturn.ReturnNumber,
                        notes: $"تعديل مرتجع مشتريات - رقم {purchaseReturn.ReturnNumber}");

                    if (movementResult.IsSuccess)
                    {
                        await _inventoryUnitOfWork.StockMovementRepository.AddAsync(movementResult.Value!);
                    }
                }
            }

            await _returnsUnitOfWork.SaveChangesAsync(cancellationToken);
            await _inventoryUnitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
