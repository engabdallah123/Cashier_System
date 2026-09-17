using FluentValidation;

namespace Inventory.Application.Batches.ProductBatches.Commands.ReplaceSupplierBatch
{
    internal sealed class ReplaceSupplierBatchCommandValidator : AbstractValidator<ReplaceSupplierBatchCommand>
    {
        public ReplaceSupplierBatchCommandValidator()
        {
            RuleFor(x => x.BatchId).NotEmpty().WithMessage("يجب تحديد دفعة المخزون.");
            RuleFor(x => x.NewExpiryDate).GreaterThan(DateTime.UtcNow).WithMessage("تاريخ الصلاحية الجديد يجب أن يكون تاريخاً مستقبلياً.");
        }
    }
}
