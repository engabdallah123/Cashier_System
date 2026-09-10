using POS.Shared.Domain;

namespace Sales.Domain.Sales.Entities
{
    public sealed class SalePayment : Entity
    {
        public Guid SaleId { get; private set; }
        public decimal Amount { get; private set; }
        public DateTime PaymentDate { get; private set; }
        public string PaymentMethod { get; private set; } = "Cash";
        public Guid CashierId { get; private set; }
        public Guid? ShiftId { get; private set; }
        public string? Notes { get; private set; }

        private SalePayment() { } // EF Core

        private SalePayment(
            Guid id,
            Guid saleId,
            decimal amount,
            DateTime paymentDate,
            string paymentMethod,
            Guid cashierId,
            Guid? shiftId,
            string? notes)
            : base(id)
        {
            SaleId = saleId;
            Amount = amount;
            PaymentDate = paymentDate;
            PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "Cash" : paymentMethod.Trim();
            CashierId = cashierId;
            ShiftId = shiftId;
            Notes = notes?.Trim();
        }

        public static Result<SalePayment> Create(
            Guid saleId,
            decimal amount,
            DateTime paymentDate,
            string paymentMethod = "Cash",
            Guid cashierId = default,
            Guid? shiftId = null,
            string? notes = null)
        {
            if (saleId == Guid.Empty)
                return Result<SalePayment>.Failure(new Error("SalePayment.SaleIdRequired", "معرّف الفاتورة مطلوب."));

            if (amount <= 0)
                return Result<SalePayment>.Failure(new Error("SalePayment.InvalidAmount", "مبلغ الدفعة يجب أن يكون أكبر من صفر."));

            var payment = new SalePayment(
                Guid.NewGuid(),
                saleId,
                amount,
                paymentDate,
                paymentMethod,
                cashierId,
                shiftId,
                notes);

            return Result<SalePayment>.Success(payment);
        }
    }
}
