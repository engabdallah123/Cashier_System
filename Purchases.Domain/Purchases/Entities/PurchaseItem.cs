using POS.Shared.Domain;

namespace Purchases.Domain.Purchases.Entities
{
    public sealed class PurchaseItem : Entity
    {
        public Guid PurchaseId { get; private set; }
        public Guid ProductId { get; private set; }
        public decimal Quantity { get; private set; }
        public decimal UnitCost { get; private set; }
        public decimal Discount { get; private set; }
        public decimal Tax { get; private set; }
        public decimal Total { get; private set; }
        public DateTime? ExpiryDate { get; private set; }
        public string? BatchNumber { get; private set; }

        private PurchaseItem() { } // EF Core

        private PurchaseItem(
            Guid id, Guid purchaseId, Guid productId, decimal quantity, decimal unitCost,
            decimal discount, decimal tax, DateTime? expiryDate, string? batchNumber)
            : base(id)
        {
            PurchaseId = purchaseId;
            ProductId = productId;
            Quantity = quantity;
            UnitCost = unitCost;
            Discount = discount;
            Tax = tax;
            Total = (quantity * unitCost) - discount + tax;
            ExpiryDate = expiryDate;
            BatchNumber = batchNumber;
        }

        public static Result<PurchaseItem> Create(
            Guid purchaseId, Guid productId, decimal quantity, decimal unitCost,
            decimal discount = 0, decimal tax = 0, DateTime? expiryDate = null, string? batchNumber = null)
        {
            if (productId == Guid.Empty)
                return Result<PurchaseItem>.Failure(PurchaseErrors.ProductIdRequired);

            if (quantity <= 0)
                return Result<PurchaseItem>.Failure(PurchaseErrors.InvalidQuantity);

            if (unitCost < 0)
                return Result<PurchaseItem>.Failure(PurchaseErrors.InvalidUnitCost);

            var item = new PurchaseItem(
                Guid.NewGuid(), purchaseId, productId, quantity, unitCost,
                discount, tax, expiryDate, batchNumber?.Trim());

            return Result<PurchaseItem>.Success(item);
        }

        public void UpdateBatchNumber(string? batchNumber)
        {
            BatchNumber = string.IsNullOrWhiteSpace(batchNumber) ? null : batchNumber.Trim();
        }
    }
}
