using POS.Shared.Domain;

namespace Inventory.Domain.Batches.Entities
{
    public enum BatchStatus
    {
        Active = 1,
        Depleted = 2
    }

    public sealed class InventoryBatch : Entity
    {
        public Guid ProductId { get; private set; }
        public Guid? PurchaseInvoiceId { get; private set; }
        public Guid? PurchaseInvoiceItemId { get; private set; }
        public string BatchNumber { get; private set; } = default!;

        public decimal OriginalQuantity { get; private set; }
        public string OriginalUnit { get; private set; } = "Piece";
        public decimal BaseQuantity { get; private set; }
        public decimal RemainingQuantity { get; private set; }
        public decimal UnitCost { get; private set; }

        public DateTime PurchaseDate { get; private set; }
        public DateTime? ExpiryDate { get; private set; }
        public BatchStatus Status { get; private set; } = BatchStatus.Active;

        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }

        private InventoryBatch() { } // EF Core

        private InventoryBatch(
            Guid id,
            Guid productId,
            Guid? purchaseInvoiceId,
            Guid? purchaseInvoiceItemId,
            string batchNumber,
            decimal originalQuantity,
            string originalUnit,
            decimal baseQuantity,
            decimal remainingQuantity,
            decimal unitCost,
            DateTime purchaseDate,
            DateTime? expiryDate)
            : base(id)
        {
            ProductId = productId;
            PurchaseInvoiceId = purchaseInvoiceId;
            PurchaseInvoiceItemId = purchaseInvoiceItemId;
            BatchNumber = batchNumber;
            OriginalQuantity = originalQuantity;
            OriginalUnit = originalUnit;
            BaseQuantity = baseQuantity;
            RemainingQuantity = remainingQuantity;
            UnitCost = unitCost;
            PurchaseDate = purchaseDate;
            ExpiryDate = expiryDate;
            Status = remainingQuantity > 0 ? BatchStatus.Active : BatchStatus.Depleted;
            CreatedAt = DateTime.UtcNow;
        }

        public static Result<InventoryBatch> Create(
            Guid productId,
            decimal originalQuantity,
            string originalUnit,
            decimal baseQuantity,
            decimal unitCost,
            DateTime purchaseDate,
            DateTime? expiryDate = null,
            string? batchNumber = null,
            Guid? purchaseInvoiceId = null,
            Guid? purchaseInvoiceItemId = null)
        {
            if (productId == Guid.Empty)
                return Result<InventoryBatch>.Failure(Error.EmptyId("Product"));

            if (baseQuantity < 0)
                return Result<InventoryBatch>.Failure(new Error("Batch.InvalidQuantity", "الكمية الأساسية للتشغيلة لا يمكن أن تكون سالبة."));

            if (unitCost < 0)
                return Result<InventoryBatch>.Failure(new Error("Batch.InvalidUnitCost", "تكلفة الوحدة للتشغيلة لا يمكن أن تكون سالبة."));

            var bNum = !string.IsNullOrWhiteSpace(batchNumber)
                ? batchNumber.Trim()
                : $"LOT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

            var batch = new InventoryBatch(
                Guid.NewGuid(),
                productId,
                purchaseInvoiceId,
                purchaseInvoiceItemId,
                bNum,
                originalQuantity,
                string.IsNullOrWhiteSpace(originalUnit) ? "Piece" : originalUnit.Trim(),
                baseQuantity,
                baseQuantity,
                unitCost,
                purchaseDate,
                expiryDate);

            return Result<InventoryBatch>.Success(batch);
        }

        public Result Deduct(decimal baseQuantityToDeduct)
        {
            if (baseQuantityToDeduct <= 0)
                return Result.Failure(new Error("Batch.InvalidDeduction", "الكمية المراد خصمها يجب أن تكون أكبر من الصفر."));

            if (baseQuantityToDeduct > RemainingQuantity)
                return Result.Failure(new Error("Batch.InsufficientRemaining", "الكمية المراد خصمها أكبر من الرصيد المتبقي في هذه الدفعة."));

            RemainingQuantity -= baseQuantityToDeduct;
            if (RemainingQuantity <= 0)
            {
                RemainingQuantity = 0;
                Status = BatchStatus.Depleted;
            }

            UpdatedAt = DateTime.UtcNow;
            return Result.Success();
        }

        public Result Restore(decimal baseQuantityToRestore)
        {
            if (baseQuantityToRestore <= 0)
                return Result.Failure(new Error("Batch.InvalidRestore", "الكمية المراد استرجاعها يجب أن تكون أكبر من الصفر."));

            RemainingQuantity += baseQuantityToRestore;
            if (RemainingQuantity > 0)
            {
                Status = BatchStatus.Active;
            }

            UpdatedAt = DateTime.UtcNow;
            return Result.Success();
        }

        public Result UpdateDetails(
            decimal originalQuantity,
            string originalUnit,
            decimal baseQuantity,
            decimal remainingQuantity,
            decimal unitCost,
            DateTime? expiryDate,
            string? batchNumber = null)
        {
            if (remainingQuantity < 0)
                return Result.Failure(new Error("Batch.InvalidRemaining", "الرصيد المتبقي لا يمكن أن يكون سالباً."));

            OriginalQuantity = originalQuantity;
            OriginalUnit = string.IsNullOrWhiteSpace(originalUnit) ? OriginalUnit : originalUnit.Trim();
            BaseQuantity = baseQuantity;
            RemainingQuantity = remainingQuantity;
            UnitCost = unitCost;
            ExpiryDate = expiryDate;
            if (!string.IsNullOrWhiteSpace(batchNumber))
                BatchNumber = batchNumber.Trim();

            Status = RemainingQuantity > 0 ? BatchStatus.Active : BatchStatus.Depleted;
            UpdatedAt = DateTime.UtcNow;
            return Result.Success();
        }
    }
}
