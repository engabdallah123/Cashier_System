using POS.Shared.Domain;
using POS.Shared.Domain.Events.Sales;

namespace Sales.Domain.Sales.Entities
{
    public sealed class Sale : Entity
    {
        private readonly List<SaleItem> _items = new();
        private readonly List<SalePayment> _payments = new();

        public string InvoiceNumber { get; private set; } = default!;
        public DateTime SaleDate { get; private set; }
        public Guid CashierId { get; private set; }
        public Guid? CustomerId { get; private set; }
        public Guid ShiftId { get; private set; }

        public decimal SubTotal { get; private set; }
        public decimal DiscountAmount { get; private set; }
        public decimal TaxAmount { get; private set; }
        public decimal TotalAmount { get; private set; }

        public decimal PaidAmount { get; private set; }
        public decimal ChangeAmount { get; private set; }
        public string PaymentMethod { get; private set; } = default!;
        public SaleStatus Status { get; private set; }
        public string? Notes { get; private set; }

        public IReadOnlyList<SaleItem> Items => _items.AsReadOnly();
        public IReadOnlyList<SalePayment> Payments => _payments.AsReadOnly();

        private Sale() { } // EF Core

        private Sale(
            Guid id, string invoiceNumber, Guid cashierId, Guid shiftId,
            Guid? customerId, decimal discountAmount, decimal taxAmount,
            decimal paidAmount, string paymentMethod, string? notes)
            : base(id)
        {
            InvoiceNumber = invoiceNumber;
            SaleDate = DateTime.UtcNow;
            CashierId = cashierId;
            ShiftId = shiftId;
            CustomerId = customerId;
            DiscountAmount = discountAmount;
            TaxAmount = taxAmount;
            PaidAmount = paidAmount;
            PaymentMethod = paymentMethod;
            Status = SaleStatus.Completed;
            Notes = notes;
        }

        public static Result<Sale> Create(
            string invoiceNumber, Guid cashierId, Guid shiftId,
            Guid? customerId = null, decimal discountAmount = 0,
            decimal taxAmount = 0, decimal paidAmount = 0,
            string paymentMethod = "Cash", string? notes = null)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber))
                return Result<Sale>.Failure(SaleErrors.InvoiceNumberRequired);

            if (cashierId == Guid.Empty)
                return Result<Sale>.Failure(SaleErrors.CashierIdRequired);

            if (shiftId == Guid.Empty)
                return Result<Sale>.Failure(SaleErrors.ShiftIdRequired);

            var sale = new Sale(
                Guid.NewGuid(), invoiceNumber.Trim(), cashierId, shiftId,
                customerId, discountAmount, taxAmount, paidAmount,
                paymentMethod.Trim(), notes?.Trim());

            return Result<Sale>.Success(sale);
        }

        public Result AddItem(Guid productId, decimal quantity, decimal unitPrice, decimal discount = 0, decimal tax = 0)
        {
            var itemResult = SaleItem.Create(Id, productId, quantity, unitPrice, discount, tax);
            if (itemResult.IsFailure)
                return itemResult;

            _items.Add(itemResult.Value!);
            CalculateTotals();
            return Result.Success();
        }

        public Result Complete()
        {
            if (!_items.Any())
                return Result.Failure(SaleErrors.SaleHasNoItems);

            CalculateTotals();

            // إذا كان المبلغ المدفوع أقل من الإجمالي، يُسمح بذلك كـ آجل / دفع جزئي فقط إذا تم تحديد عميل، أو كانت طريقة الدفع آجل (Credit)
            if (PaidAmount + 0.01m < TotalAmount && !PaymentMethod.Equals("Credit", StringComparison.OrdinalIgnoreCase) && !CustomerId.HasValue)
                return Result.Failure(SaleErrors.CustomerRequiredForCredit);

            ChangeAmount = PaidAmount > TotalAmount ? PaidAmount - TotalAmount : 0;
            Status = SaleStatus.Completed;

            // تسجيل الدفعة الأولية إذا كانت الفاتورة مسددة كلياً أو جزئياً عند الإنشاء
            if (PaidAmount > 0 && !_payments.Any())
            {
                var initialPayment = SalePayment.Create(
                    Id,
                    Math.Min(PaidAmount, TotalAmount),
                    SaleDate,
                    PaymentMethod,
                    CashierId,
                    ShiftId,
                    "دفعة أولية عند البيع");

                if (initialPayment.IsSuccess)
                {
                    _payments.Add(initialPayment.Value!);
                }
            }

            RaiseDomainEvent(new SaleCompletedIntegrationEvent(Id, ShiftId, TotalAmount, PaymentMethod));
            return Result.Success();
        }

        public Result<SalePayment> AddPayment(
            decimal amount,
            DateTime? paymentDate = null,
            string paymentMethod = "Cash",
            Guid? cashierId = null,
            Guid? shiftId = null,
            string? notes = null)
        {
            if (amount <= 0)
                return Result<SalePayment>.Failure(new Error("Sale.InvalidPaymentAmount", "مبلغ السداد يجب أن يكون أكبر من صفر."));

            var remaining = TotalAmount - PaidAmount;
            if (remaining <= 0.001m)
                return Result<SalePayment>.Failure(new Error("Sale.AlreadyFullyPaid", "الفاتورة مسددة بالكامل بالفعل."));

            if (amount > remaining + 0.01m)
                return Result<SalePayment>.Failure(new Error("Sale.PaymentExceedsRemaining", $"مبلغ السداد ({amount:N2} ج.م) أكبر من المبلغ المتبقي على الفاتورة ({remaining:N2} ج.م)."));

            PaidAmount += amount;
            ChangeAmount = PaidAmount > TotalAmount ? PaidAmount - TotalAmount : 0;

            var paymentResult = SalePayment.Create(
                Id,
                amount,
                paymentDate ?? DateTime.UtcNow,
                paymentMethod,
                cashierId ?? CashierId,
                shiftId ?? ShiftId,
                notes);

            if (paymentResult.IsFailure)
                return Result<SalePayment>.Failure(paymentResult.Error);

            var payment = paymentResult.Value!;
            _payments.Add(payment);

            return Result<SalePayment>.Success(payment);
        }

        public Result Cancel()
        {
            if (Status == SaleStatus.Cancelled)
                return Result.Failure(SaleErrors.AlreadyCancelled);

            Status = SaleStatus.Cancelled;
            RaiseDomainEvent(new SaleCancelledIntegrationEvent(Id, ShiftId, TotalAmount));
            return Result.Success();
        }

        private void CalculateTotals()
        {
            SubTotal = _items.Sum(i => i.Quantity * i.UnitPrice);
            TotalAmount = Math.Max(0, SubTotal - DiscountAmount + TaxAmount);
            ChangeAmount = PaidAmount > TotalAmount ? PaidAmount - TotalAmount : 0;
        }
    }
}
