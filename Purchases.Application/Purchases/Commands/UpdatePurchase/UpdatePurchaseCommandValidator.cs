using FluentValidation;

namespace Purchases.Application.Purchases.Commands.UpdatePurchase
{
    internal sealed class UpdatePurchaseCommandValidator : AbstractValidator<UpdatePurchaseCommand>
    {
        public UpdatePurchaseCommandValidator()
        {
            RuleFor(x => x.Id).NotEmpty().WithMessage("معرف الفاتورة مطلوب.");
            RuleFor(x => x.InvoiceNumber).NotEmpty().WithMessage("رقم الفاتورة مطلوب.");
            RuleFor(x => x.SupplierId).NotEmpty().WithMessage("المورد مطلوب.");
            RuleFor(x => x.Items).NotEmpty().WithMessage("يجب إضافة عنصر واحد على الأقل في الفاتورة.");
        }
    }
}
