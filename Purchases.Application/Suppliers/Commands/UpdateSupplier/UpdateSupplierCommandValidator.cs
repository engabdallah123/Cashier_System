using FluentValidation;

namespace Purchases.Application.Suppliers.Commands.UpdateSupplier
{
    internal sealed class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
    {
        public UpdateSupplierCommandValidator()
        {
            RuleFor(x => x.Id).NotEmpty().WithMessage("معرف المورد مطلوب.");
            RuleFor(x => x.Name).NotEmpty().WithMessage("اسم المورد مطلوب.");
            RuleFor(x => x.Phone).NotEmpty().WithMessage("رقم هاتف المورد مطلوب.");
        }
    }
}
