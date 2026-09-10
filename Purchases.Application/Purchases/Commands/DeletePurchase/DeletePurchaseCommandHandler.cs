using Inventory.Domain;
using Inventory.Domain.Stock.StockMovements;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;
using Purchases.Domain;
using Purchases.Domain.Purchases;
using Purchases.Domain.Purchases.Entities;

namespace Purchases.Application.Purchases.Commands.DeletePurchase
{
    internal sealed class DeletePurchaseCommandHandler : ICommandHandler<DeletePurchaseCommand>
    {
        private readonly IPurchasesUnitOfWork _purchasesUnitOfWork;
        private readonly IInventoryUnitOfWork _inventoryUnitOfWork;

        public DeletePurchaseCommandHandler(
            IPurchasesUnitOfWork purchasesUnitOfWork,
            IInventoryUnitOfWork inventoryUnitOfWork)
        {
            _purchasesUnitOfWork = purchasesUnitOfWork;
            _inventoryUnitOfWork = inventoryUnitOfWork;
        }

        public async Task<Result> Handle(DeletePurchaseCommand request, CancellationToken cancellationToken)
        {
            var purchase = await _purchasesUnitOfWork.PurchaseRepository.FindAsync(
                p => p.Id == request.Id,
                new[] { "Items" });

            if (purchase is null)
                return Result.Failure(PurchaseErrors.NotFound(request.Id));

            // إذا كانت الفاتورة مستلمة، نقوم بخصم الكميات والدفعات من المخزن التي كانت قد أضيفت سابقاً
            if (purchase.Status == PurchaseStatus.Received)
            {
                var batches = await _inventoryUnitOfWork.BatchRepository.GetBatchesByPurchaseIdAsync(purchase.Id, cancellationToken);
                foreach (var item in purchase.Items)
                {
                    var product = await _inventoryUnitOfWork.ProductRepository.GetByIdAsync(item.ProductId, cancellationToken);
                    if (product is not null)
                    {
                        var batch = batches.FirstOrDefault(b => b.PurchaseInvoiceItemId == item.Id);
                        decimal qtyToDeduct = batch?.BaseQuantity ?? item.Quantity;

                        product.AdjustStock(-qtyToDeduct, allowNegativeStock: true);
                        _inventoryUnitOfWork.ProductRepository.Update(product);

                        if (batch is not null)
                        {
                            _inventoryUnitOfWork.BatchRepository.Delete(batch);
                        }

                        var movementResult = StockMovement.Create(
                            item.ProductId,
                            -qtyToDeduct,
                            StockMovementType.Adjustment,
                            purchase.CreatedByUserId,
                            reference: purchase.InvoiceNumber,
                            notes: $"حذف فاتورة شراء - رقم {purchase.InvoiceNumber}");

                        if (movementResult.IsSuccess)
                        {
                            await _inventoryUnitOfWork.StockMovementRepository.AddAsync(movementResult.Value!);
                        }
                    }
                }
            }

            _purchasesUnitOfWork.PurchaseRepository.Delete(purchase);

            await _purchasesUnitOfWork.SaveChangesAsync(cancellationToken);
            await _inventoryUnitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
