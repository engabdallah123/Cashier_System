using POS.Shared.Domain;

namespace Purchases.Domain.Purchases.Entities
{
    public sealed class Purchase : Entity
    {
        private readonly List<PurchaseItem> _items = new();

        public string InvoiceNumber { get; private set; } = default!;
        public string? InternalNumber { get; private set; }
        public Guid SupplierId { get; private set; }

        public DateTime PurchaseDate { get; private set; }
        public DateTime? ReceivedDate { get; private set; }

        public decimal SubTotal { get; private set; }
        public decimal DiscountAmount { get; private set; }
        public decimal TaxAmount { get; private set; }
        public decimal TotalAmount { get; private set; }

        public decimal PaidAmount { get; private set; }
        public decimal RemainingAmount { get; private set; }
        public PaymentMethod PaymentMethod { get; private set; }
        public PurchaseStatus Status { get; private set; }

        public string? Notes { get; private set; }
        public Guid CreatedByUserId { get; private set; }

        public IReadOnlyList<PurchaseItem> Items => _items.AsReadOnly();

        private Purchase() { } // EF Core

        private Purchase(
            Guid id, string invoiceNumber, string? internalNumber, Guid supplierId,
            decimal discountAmount, decimal taxAmount, decimal paidAmount,
            PaymentMethod paymentMethod, string? notes, Guid createdByUserId,
            DateTime? purchaseDate = null)
            : base(id)
        {
            InvoiceNumber = invoiceNumber;
            InternalNumber = internalNumber;
            SupplierId = supplierId;
            PurchaseDate = purchaseDate ?? DateTime.UtcNow;
            DiscountAmount = discountAmount;
            TaxAmount = taxAmount;
            PaidAmount = paidAmount;
            PaymentMethod = paymentMethod;
            Status = PurchaseStatus.Draft;
            Notes = notes;
            CreatedByUserId = createdByUserId;
        }

        public static Result<Purchase> Create(
            string invoiceNumber, Guid supplierId, Guid createdByUserId,
            string? internalNumber = null, decimal discountAmount = 0,
            decimal taxAmount = 0, decimal paidAmount = 0,
            PaymentMethod paymentMethod = PaymentMethod.Cash, string? notes = null,
            DateTime? purchaseDate = null)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber))
                return Result<Purchase>.Failure(PurchaseErrors.InvoiceNumberRequired);

            if (supplierId == Guid.Empty)
                return Result<Purchase>.Failure(PurchaseErrors.SupplierIdRequired);

            if (createdByUserId == Guid.Empty)
                return Result<Purchase>.Failure(PurchaseErrors.CreatedByRequired);

            var purchase = new Purchase(
                Guid.NewGuid(), invoiceNumber.Trim(), internalNumber?.Trim(),
                supplierId, discountAmount, taxAmount, paidAmount,
                paymentMethod, notes?.Trim(), createdByUserId, purchaseDate);

            return Result<Purchase>.Success(purchase);
        }

        public Result UpdateDetails(
            string invoiceNumber, Guid supplierId, string? internalNumber,
            decimal discountAmount, decimal taxAmount, decimal paidAmount,
            PaymentMethod paymentMethod, string? notes, DateTime? purchaseDate = null)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber))
                return Result.Failure(PurchaseErrors.InvoiceNumberRequired);

            if (supplierId == Guid.Empty)
                return Result.Failure(PurchaseErrors.SupplierIdRequired);

            InvoiceNumber = invoiceNumber.Trim();
            InternalNumber = internalNumber?.Trim();
            SupplierId = supplierId;
            if (purchaseDate.HasValue)
            {
                PurchaseDate = purchaseDate.Value;
            }
            DiscountAmount = discountAmount;
            TaxAmount = taxAmount;
            PaidAmount = paidAmount;
            PaymentMethod = paymentMethod;
            Notes = notes?.Trim();

            CalculateTotals();
            return Result.Success();
        }

        public void ClearItems()
        {
            _items.Clear();
            CalculateTotals();
        }

        public Result<PurchaseItem> AddItemDirect(Guid productId, decimal quantity, decimal unitCost, decimal discount = 0, decimal tax = 0, DateTime? expiryDate = null, string? batchNumber = null)
        {
            var itemResult = PurchaseItem.Create(Id, productId, quantity, unitCost, discount, tax, expiryDate, batchNumber);
            if (itemResult.IsFailure)
                return Result<PurchaseItem>.Failure(itemResult.Error);

            _items.Add(itemResult.Value!);
            CalculateTotals();
            return Result<PurchaseItem>.Success(itemResult.Value!);
        }

        public Result AddItem(Guid productId, decimal quantity, decimal unitCost, decimal discount = 0, decimal tax = 0, DateTime? expiryDate = null, string? batchNumber = null)
        {
            if (Status != PurchaseStatus.Draft && Status != PurchaseStatus.Received)
                return Result.Failure(PurchaseErrors.OnlyDraftCanBeModified);

            var itemResult = PurchaseItem.Create(Id, productId, quantity, unitCost, discount, tax, expiryDate, batchNumber);
            if (itemResult.IsFailure)
                return itemResult;

            _items.Add(itemResult.Value!);
            CalculateTotals();
            return Result.Success();
        }

        public Result Receive()
        {
            if (Status != PurchaseStatus.Draft)
                return Result.Failure(PurchaseErrors.OnlyDraftCanBeReceived);

            if (!_items.Any())
                return Result.Failure(PurchaseErrors.PurchaseHasNoItems);

            Status = PurchaseStatus.Received;
            ReceivedDate = DateTime.UtcNow;
            return Result.Success();
        }

        public Result Cancel()
        {
            if (Status == PurchaseStatus.Received)
                return Result.Failure(PurchaseErrors.ReceivedCannotBeCancelled);

            Status = PurchaseStatus.Cancelled;
            return Result.Success();
        }

        public Result AddPayment(decimal amount)
        {
            if (amount <= 0)
                return Result.Failure(PurchaseErrors.PaymentAmountInvalid);

            if (RemainingAmount <= 0)
                return Result.Failure(PurchaseErrors.PurchaseAlreadyFullyPaid);

            if (amount > RemainingAmount)
                return Result.Failure(PurchaseErrors.PaymentExceedsRemaining);

            PaidAmount += amount;
            RemainingAmount = TotalAmount - PaidAmount;
            return Result.Success();
        }

        private void CalculateTotals()
        {
            SubTotal = _items.Sum(i => i.Total);
            TotalAmount = SubTotal - DiscountAmount + TaxAmount;
            RemainingAmount = TotalAmount - PaidAmount;
        }
    }
}
