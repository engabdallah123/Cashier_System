using FluentValidation;

namespace Returns.Application.SalesReturns.Commands.CreateSalesReturn
{
    internal sealed class CreateSalesReturnCommandValidator : AbstractValidator<CreateSalesReturnCommand>
    {
        public CreateSalesReturnCommandValidator()
        {
            RuleFor(x => x.CashierId).NotEmpty().WithMessage("معرف الكاشير مطلوب.");
            RuleFor(x => x.ShiftId).NotEmpty().WithMessage("معرف الشفت مطلوب.");
            RuleFor(x => x.Items).NotEmpty().WithMessage("مرتجع المبيعات يجب أن يحتوي على عنصر واحد على الأقل.");
        }
    }
}
