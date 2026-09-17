using POS.Shared.Domain;

namespace Inventory.Domain.Batches.ProductBatches
{
    public sealed class ProductBatch : Entity
    {
        public Guid ProductId { get; private set; }
        public Guid WarehouseId { get; private set; }
        public string BatchNumber { get; private set; } = default!;
        public DateTime? ExpiryDate { get; private set; }
        public int Quantity { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private ProductBatch() { } // EF Core

        private ProductBatch(Guid id, Guid productId, Guid warehouseId,
            string batchNumber, DateTime? expiryDate, int quantity)
            : base(id)
        {
            ProductId = productId;
            WarehouseId = warehouseId;
            BatchNumber = batchNumber;
            ExpiryDate = expiryDate;
            Quantity = quantity;
            CreatedAt = DateTime.UtcNow;
        }

        public static Result<ProductBatch> Create(
            Guid productId, Guid warehouseId, string batchNumber,
            DateTime? expiryDate = null, int quantity = 0)
        {
            if (productId == Guid.Empty)
                return Result<ProductBatch>.Failure(Error.EmptyId("Product"));

            if (warehouseId == Guid.Empty)
                return Result<ProductBatch>.Failure(Error.EmptyId("Warehouse"));

            if (string.IsNullOrWhiteSpace(batchNumber))
                return Result<ProductBatch>.Failure(ProductBatchErrors.BatchNumberRequired);

            if (quantity < 0)
                return Result<ProductBatch>.Failure(ProductBatchErrors.QuantityCannotBeNegative);

            var batch = new ProductBatch(
                Guid.NewGuid(), productId, warehouseId,
                batchNumber.Trim(), expiryDate, quantity);

            return Result<ProductBatch>.Success(batch);
        }

        public Result IncreaseQuantity(int amount)
        {
            if (amount <= 0)
                return Result.Failure(ProductBatchErrors.QuantityMustBePositive);

            Quantity += amount;
            return Result.Success();
        }

        public Result DecreaseQuantity(int amount)
        {
            if (amount <= 0)
                return Result.Failure(ProductBatchErrors.QuantityMustBePositive);

            if (Quantity < amount)
                return Result.Failure(ProductBatchErrors.InsufficientBatchQuantity);

            Quantity -= amount;
            return Result.Success();
        }

        public bool IsExpired() => ExpiryDate.HasValue && ExpiryDate.Value.Date <= DateTime.UtcNow.Date;

        public bool IsExpiringSoon(int daysThreshold = 30)
            => ExpiryDate.HasValue && !IsExpired()
               && ExpiryDate.Value.Date <= DateTime.UtcNow.Date.AddDays(daysThreshold);

        public Result ReplaceBySupplier(DateTime newExpiryDate, string? newBatchNumber = null)
        {
            if (newExpiryDate.Date <= DateTime.UtcNow.Date)
                return Result.Failure(new Error("Batch.InvalidExpiryDate", "تاريخ الصلاحية الجديد يجب أن يكون تاريخاً مستقبلياً."));

            ExpiryDate = newExpiryDate;
            if (!string.IsNullOrWhiteSpace(newBatchNumber))
            {
                BatchNumber = newBatchNumber.Trim();
            }
            return Result.Success();
        }
    }
}
