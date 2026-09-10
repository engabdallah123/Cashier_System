using FluentValidation;

namespace Inventory.Application.Stock.Waste.Commands.RecordWaste
{
    internal sealed class RecordWasteCommandValidator : AbstractValidator<RecordWasteCommand>
    {
        public RecordWasteCommandValidator()
        {
            RuleFor(x => x.ProductId).NotEmpty().WithMessage("يجب تحديد المنتج.");
            RuleFor(x => x.InventoryBatchId).NotEmpty().WithMessage("يجب تحديد الدفعة.");
            RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("يجب أن تكون كمية الهالك أكبر من الصفر.");
            RuleFor(x => x.Unit).NotEmpty().WithMessage("يجب تحديد الوحدة.");
            RuleFor(x => x.Reason).NotEmpty().WithMessage("يجب تحديد سبب الهالك.");
        }
    }
}
