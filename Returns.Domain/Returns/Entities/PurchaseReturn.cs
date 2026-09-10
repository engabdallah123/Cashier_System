using POS.Shared.Domain;
using POS.Shared.Domain.Events.Returns;

namespace Returns.Domain.Returns.Entities
{
    public sealed class PurchaseReturn : Entity
    {
        private readonly List<PurchaseReturnItem> _items = new();

        public string ReturnNumber { get; private set; } = default!;
        public Guid OriginalPurchaseId { get; private set; }
        public Guid SupplierId { get; private set; }
        public DateTime ReturnDate { get; private set; }

        public decimal SubTotal { get; private set; }
        public decimal TaxAmount { get; private set; }
        public decimal TotalAmount { get; private set; }

        public string? Reason { get; private set; }
        public string? Notes { get; private set; }
        public ReturnStatus Status { get; private set; }
        public Guid CreatedByUserId { get; private set; }

        public IReadOnlyList<PurchaseReturnItem> Items => _items.AsReadOnly();

        private PurchaseReturn() { } // EF Core

        private PurchaseReturn(
            Guid id, string returnNumber, Guid originalPurchaseId, Guid supplierId,
            Guid createdByUserId, string? reason, string? notes)
            : base(id)
        {
            ReturnNumber = returnNumber;
            OriginalPurchaseId = originalPurchaseId;
            SupplierId = supplierId;
            CreatedByUserId = createdByUserId;
            ReturnDate = DateTime.UtcNow;
            Reason = reason;
            Notes = notes;
            Status = ReturnStatus.Completed;
        }

        public static Result<PurchaseReturn> Create(
            string returnNumber, Guid originalPurchaseId, Guid supplierId, Guid createdByUserId,
            string? reason = null, string? notes = null)
        {
            if (string.IsNullOrWhiteSpace(returnNumber))
                return Result<PurchaseReturn>.Failure(ReturnErrors.ReturnNumberRequired);

            if (originalPurchaseId == Guid.Empty)
                return Result<PurchaseReturn>.Failure(ReturnErrors.OriginalPurchaseIdRequired);

            if (supplierId == Guid.Empty)
                return Result<PurchaseReturn>.Failure(ReturnErrors.SupplierIdRequired);

            var purchaseReturn = new PurchaseReturn(
                Guid.NewGuid(), returnNumber.Trim(), originalPurchaseId, supplierId,
                createdByUserId, reason?.Trim(), notes?.Trim());

            return Result<PurchaseReturn>.Success(purchaseReturn);
        }

        public Result<PurchaseReturnItem> AddItem(Guid productId, decimal quantity, decimal unitCost, decimal tax = 0)
        {
            var itemResult = PurchaseReturnItem.Create(Id, productId, quantity, unitCost, tax);
            if (itemResult.IsFailure)
                return Result<PurchaseReturnItem>.Failure(itemResult.Error);

            _items.Add(itemResult.Value!);
            CalculateTotals();
            return Result<PurchaseReturnItem>.Success(itemResult.Value!);
        }

        public void ClearItems()
        {
            _items.Clear();
            CalculateTotals();
        }

        public Result UpdateDetails(string? reason, string? notes)
        {
            Reason = reason?.Trim();
            Notes = notes?.Trim();
            return Result.Success();
        }

        public Result Complete()
        {
            if (!_items.Any())
                return Result.Failure(ReturnErrors.ReturnHasNoItems);

            Status = ReturnStatus.Completed;
            RaiseDomainEvent(new PurchaseReturnCompletedIntegrationEvent(Id, OriginalPurchaseId, TotalAmount));
            return Result.Success();
        }

        private void CalculateTotals()
        {
            SubTotal = _items.Sum(i => i.Quantity * i.UnitCost);
            TaxAmount = _items.Sum(i => i.Tax);
            TotalAmount = SubTotal + TaxAmount;
        }
    }
}
