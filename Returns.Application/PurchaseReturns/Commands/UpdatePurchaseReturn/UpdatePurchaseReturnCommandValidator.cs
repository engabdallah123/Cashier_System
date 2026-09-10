using FluentValidation;

namespace Returns.Application.PurchaseReturns.Commands.UpdatePurchaseReturn
{
    internal sealed class UpdatePurchaseReturnCommandValidator : AbstractValidator<UpdatePurchaseReturnCommand>
    {
        public UpdatePurchaseReturnCommandValidator()
        {
            RuleFor(x => x.Id).NotEmpty().WithMessage("معرف المرتجع مطلوب.");
            RuleFor(x => x.Items).NotEmpty().WithMessage("يجب إضافة أصناف في المرتجع.");
            RuleForEach(x => x.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.ProductId).NotEmpty().WithMessage("معرف المنتج مطلوب.");
                item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("كمية المرتجع يجب أن تكون أكبر من صفر.");
                item.RuleFor(i => i.UnitCost).GreaterThanOrEqualTo(0).WithMessage("سعر التكلفة غير صالح.");
            });
        }
    }
}
