using POS.Shared.Domain;

namespace Inventory.Domain.Notifications.Entities
{
    public sealed class ExpiryNotification : Entity
    {
        public Guid ProductId { get; private set; }
        public Guid BatchId { get; private set; }
        public string Type { get; private set; } = "ExpiryAlert";
        public string Status { get; private set; } = "Active"; // "Active", "Snoozed", "Resolved"

        public string Message { get; private set; } = default!;
        public decimal RemainingQuantityAtCreation { get; private set; }
        public int DaysRemainingAtCreation { get; private set; }

        public DateTime? SnoozedUntil { get; private set; }
        public DateTime? ResolvedAt { get; private set; }
        public Guid? ResolvedBy { get; private set; }
        public Guid? RelatedWasteId { get; private set; }

        public DateTime CreatedAt { get; private set; }

        private ExpiryNotification() { } // EF Core

        private ExpiryNotification(
            Guid id,
            Guid productId,
            Guid batchId,
            string type,
            string message,
            decimal remainingQuantityAtCreation,
            int daysRemainingAtCreation)
            : base(id)
        {
            ProductId = productId;
            BatchId = batchId;
            Type = type;
            Status = "Active";
            Message = message;
            RemainingQuantityAtCreation = remainingQuantityAtCreation;
            DaysRemainingAtCreation = daysRemainingAtCreation;
            CreatedAt = DateTime.UtcNow;
        }

        public static Result<ExpiryNotification> Create(
            Guid productId,
            Guid batchId,
            string message,
            decimal remainingQuantity,
            int daysRemaining,
            string type = "ExpiryAlert")
        {
            if (productId == Guid.Empty)
                return Result<ExpiryNotification>.Failure(Error.EmptyId("Product"));

            if (batchId == Guid.Empty)
                return Result<ExpiryNotification>.Failure(Error.EmptyId("Batch"));

            var notif = new ExpiryNotification(
                Guid.NewGuid(),
                productId,
                batchId,
                string.IsNullOrWhiteSpace(type) ? "ExpiryAlert" : type.Trim(),
                message,
                remainingQuantity,
                daysRemaining);

            return Result<ExpiryNotification>.Success(notif);
        }

        public Result Resolve(Guid? resolvedBy = null)
        {
            Status = "Resolved";
            ResolvedAt = DateTime.UtcNow;
            ResolvedBy = resolvedBy;
            SnoozedUntil = null;
            return Result.Success();
        }

        public Result Snooze(int hours = 24)
        {
            Status = "Snoozed";
            SnoozedUntil = DateTime.UtcNow.AddHours(hours > 0 ? hours : 24);
            return Result.Success();
        }

        public Result Reactivate(decimal currentRemainingQuantity, int currentDaysRemaining, string? updatedMessage = null)
        {
            Status = "Active";
            SnoozedUntil = null;
            RemainingQuantityAtCreation = currentRemainingQuantity;
            DaysRemainingAtCreation = currentDaysRemaining;
            if (!string.IsNullOrWhiteSpace(updatedMessage))
                Message = updatedMessage;
            return Result.Success();
        }

        public Result LinkWaste(Guid wasteId)
        {
            RelatedWasteId = wasteId;
            Status = "Resolved";
            ResolvedAt = DateTime.UtcNow;
            return Result.Success();
        }
    }
}
