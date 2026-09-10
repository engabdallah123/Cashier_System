using POS.Shared.Domain;
using POS.Shared.Domain.Events.Returns;

namespace Returns.Domain.Returns.Entities
{
    public sealed class SalesReturn : Entity
    {
        private readonly List<SalesReturnItem> _items = new();

        public string ReturnNumber { get; private set; } = default!;
        public Guid OriginalSaleId { get; private set; }
        public Guid CashierId { get; private set; }
        public Guid? CustomerId { get; private set; }
        public Guid ShiftId { get; private set; }
        public DateTime ReturnDate { get; private set; }

        public decimal SubTotal { get; private set; }
        public decimal TaxAmount { get; private set; }
        public decimal TotalAmount { get; private set; }
        public RefundMethod RefundMethod { get; private set; }

        public string? Reason { get; private set; }
        public string? Notes { get; private set; }
        public ReturnStatus Status { get; private set; }

        public IReadOnlyList<SalesReturnItem> Items => _items.AsReadOnly();

        private SalesReturn() { } // EF Core

        private SalesReturn(
            Guid id, string returnNumber, Guid originalSaleId, Guid cashierId,
            Guid shiftId, Guid? customerId, RefundMethod refundMethod,
            string? reason, string? notes)
            : base(id)
        {
            ReturnNumber = returnNumber;
            OriginalSaleId = originalSaleId;
            CashierId = cashierId;
            ShiftId = shiftId;
            CustomerId = customerId;
            ReturnDate = DateTime.UtcNow;
            RefundMethod = refundMethod;
            Reason = reason;
            Notes = notes;
            Status = ReturnStatus.Completed;
        }

        public static Result<SalesReturn> Create(
            string returnNumber, Guid originalSaleId, Guid cashierId, Guid shiftId,
            Guid? customerId = null, RefundMethod refundMethod = RefundMethod.Cash,
            string? reason = null, string? notes = null)
        {
            if (string.IsNullOrWhiteSpace(returnNumber))
                return Result<SalesReturn>.Failure(ReturnErrors.ReturnNumberRequired);

            if (cashierId == Guid.Empty)
                return Result<SalesReturn>.Failure(ReturnErrors.CashierIdRequired);

            if (shiftId == Guid.Empty)
                return Result<SalesReturn>.Failure(ReturnErrors.ShiftIdRequired);

            var salesReturn = new SalesReturn(
                Guid.NewGuid(), returnNumber.Trim(), originalSaleId, cashierId,
                shiftId, customerId, refundMethod, reason?.Trim(), notes?.Trim());

            return Result<SalesReturn>.Success(salesReturn);
        }

        public Result<SalesReturnItem> AddItem(Guid productId, Guid originalSaleItemId, decimal quantity, decimal unitPrice, decimal tax = 0, string? reason = null)
        {
            var itemResult = SalesReturnItem.Create(Id, productId, originalSaleItemId, quantity, unitPrice, tax, reason);
            if (itemResult.IsFailure)
                return Result<SalesReturnItem>.Failure(itemResult.Error);

            _items.Add(itemResult.Value!);
            CalculateTotals();
            return Result<SalesReturnItem>.Success(itemResult.Value!);
        }

        public void ClearItems()
        {
            _items.Clear();
            CalculateTotals();
        }

        public Result UpdateDetails(RefundMethod refundMethod, string? reason, string? notes)
        {
            RefundMethod = refundMethod;
            Reason = reason?.Trim();
            Notes = notes?.Trim();
            return Result.Success();
        }

        public Result Complete()
        {
            if (!_items.Any())
                return Result.Failure(ReturnErrors.ReturnHasNoItems);

            Status = ReturnStatus.Completed;
            RaiseDomainEvent(new SalesReturnCompletedIntegrationEvent(Id, OriginalSaleId, ShiftId, TotalAmount, RefundMethod.ToString()));
            return Result.Success();
        }

        private void CalculateTotals()
        {
            SubTotal = _items.Sum(i => i.Quantity * i.UnitPrice);
            TaxAmount = _items.Sum(i => i.Tax);
            TotalAmount = SubTotal + TaxAmount;
        }
    }
}
