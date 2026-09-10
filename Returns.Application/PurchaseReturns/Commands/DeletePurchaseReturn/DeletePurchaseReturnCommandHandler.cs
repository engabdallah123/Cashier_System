using Inventory.Domain;
using Inventory.Domain.Stock.StockMovements;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Returns.Domain;
using Returns.Domain.Returns;

namespace Returns.Application.PurchaseReturns.Commands.DeletePurchaseReturn
{
    internal sealed class DeletePurchaseReturnCommandHandler : ICommandHandler<DeletePurchaseReturnCommand>
    {
        private readonly IReturnsUnitOfWork _returnsUnitOfWork;
        private readonly IInventoryUnitOfWork _inventoryUnitOfWork;

        public DeletePurchaseReturnCommandHandler(
            IReturnsUnitOfWork returnsUnitOfWork,
            IInventoryUnitOfWork inventoryUnitOfWork)
        {
            _returnsUnitOfWork = returnsUnitOfWork;
            _inventoryUnitOfWork = inventoryUnitOfWork;
        }

        public async Task<Result> Handle(DeletePurchaseReturnCommand request, CancellationToken cancellationToken)
        {
            var purchaseReturn = await _returnsUnitOfWork.PurchaseReturnRepository.FindAsync(
                pr => pr.Id == request.Id,
                new[] { "Items" });

            if (purchaseReturn is null)
                return Result.Failure(ReturnErrors.NotFound(request.Id));

            // 1. Revert stock: increase stock back for each returned item and log adjustment movement
            foreach (var item in purchaseReturn.Items)
            {
                var product = await _inventoryUnitOfWork.ProductRepository.GetByIdAsync(item.ProductId, cancellationToken);
                if (product is not null)
                {
                    product.AdjustStock(item.Quantity, allowNegativeStock: true);
                    _inventoryUnitOfWork.ProductRepository.Update(product);

                    var movementResult = StockMovement.Create(
                        item.ProductId,
                        item.Quantity,
                        StockMovementType.Adjustment,
                        request.UserId,
                        reference: purchaseReturn.ReturnNumber,
                        notes: $"حذف مرتجع مشتريات - رقم {purchaseReturn.ReturnNumber}");

                    if (movementResult.IsSuccess)
                    {
                        await _inventoryUnitOfWork.StockMovementRepository.AddAsync(movementResult.Value!);
                    }
                }
            }

            // 2. Delete items & return entity
            var itemsList = purchaseReturn.Items.ToList();
            if (itemsList.Any())
            {
                _returnsUnitOfWork.PurchaseReturnItemRepository.DeleteRange(itemsList);
            }

            _returnsUnitOfWork.PurchaseReturnRepository.Delete(purchaseReturn);

            await _returnsUnitOfWork.SaveChangesAsync(cancellationToken);
            await _inventoryUnitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
