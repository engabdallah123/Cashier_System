using POS.Shared.Domain;

namespace Inventory.Domain.Stock.Waste.Entities
{
    public sealed class InventoryWaste : Entity
    {
        public Guid ProductId { get; private set; }
        public Guid InventoryBatchId { get; private set; }
        public Guid? PurchaseInvoiceId { get; private set; }
        public Guid? PurchaseInvoiceItemId { get; private set; }

        public decimal Quantity { get; private set; }
        public string Unit { get; private set; } = "Piece";
        public decimal BaseQuantity { get; private set; }
        public decimal UnitCost { get; private set; }
        public decimal TotalCost { get; private set; }

        public string Reason { get; private set; } = "Expired";
        public string Source { get; private set; } = "Manual";
        public Guid? RelatedNotificationId { get; private set; }

        public string? Notes { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public Guid CreatedBy { get; private set; }

        private InventoryWaste() { } // EF Core

        private InventoryWaste(
            Guid id,
            Guid productId,
            Guid inventoryBatchId,
            Guid? purchaseInvoiceId,
            Guid? purchaseInvoiceItemId,
            decimal quantity,
            string unit,
            decimal baseQuantity,
            decimal unitCost,
            decimal totalCost,
            string reason,
            string source,
            Guid? relatedNotificationId,
            string? notes,
            Guid createdBy)
            : base(id)
        {
            ProductId = productId;
            InventoryBatchId = inventoryBatchId;
            PurchaseInvoiceId = purchaseInvoiceId;
            PurchaseInvoiceItemId = purchaseInvoiceItemId;
            Quantity = quantity;
            Unit = unit;
            BaseQuantity = baseQuantity;
            UnitCost = unitCost;
            TotalCost = totalCost;
            Reason = reason;
            Source = source;
            RelatedNotificationId = relatedNotificationId;
            Notes = notes;
            CreatedAt = DateTime.UtcNow;
            CreatedBy = createdBy;
        }

        public static Result<InventoryWaste> Create(
            Guid productId,
            Guid inventoryBatchId,
            decimal quantity,
            string unit,
            decimal baseQuantity,
            decimal unitCost,
            string reason,
            string source,
            Guid createdBy,
            Guid? purchaseInvoiceId = null,
            Guid? purchaseInvoiceItemId = null,
            Guid? relatedNotificationId = null,
            string? notes = null)
        {
            if (productId == Guid.Empty)
                return Result<InventoryWaste>.Failure(Error.EmptyId("Product"));

            if (inventoryBatchId == Guid.Empty)
                return Result<InventoryWaste>.Failure(Error.EmptyId("InventoryBatch"));

            if (quantity <= 0 || baseQuantity <= 0)
                return Result<InventoryWaste>.Failure(new Error("Waste.InvalidQuantity", "كمية الهالك يجب أن تكون أكبر من الصفر."));

            if (unitCost < 0)
                return Result<InventoryWaste>.Failure(new Error("Waste.InvalidUnitCost", "تكلفة شراء الوحدة لا يمكن أن تكون سالبة."));

            var totalCost = Math.Round(baseQuantity * unitCost, 2);

            var waste = new InventoryWaste(
                Guid.NewGuid(),
                productId,
                inventoryBatchId,
                purchaseInvoiceId,
                purchaseInvoiceItemId,
                quantity,
                string.IsNullOrWhiteSpace(unit) ? "Piece" : unit.Trim(),
                baseQuantity,
                unitCost,
                totalCost,
                string.IsNullOrWhiteSpace(reason) ? "Expired" : reason.Trim(),
                string.IsNullOrWhiteSpace(source) ? "Manual" : source.Trim(),
                relatedNotificationId,
                notes?.Trim(),
                createdBy);

            return Result<InventoryWaste>.Success(waste);
        }
    }
}
