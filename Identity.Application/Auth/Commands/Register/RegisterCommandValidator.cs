using FluentValidation;

namespace Identity.Application.Auth.Commands.Register
{
    internal sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
    {
        private static readonly string[] AllowedRoles = { "Admin", "Manager", "Cashier" };

        public RegisterCommandValidator()
        {
            RuleFor(x => x.FullName).NotEmpty().MaximumLength(100).WithMessage("الاسم الكامل مطلوب ولا يتجاوز 100 حرف.");
            RuleFor(x => x.UserName).NotEmpty().MaximumLength(50).WithMessage("اسم المستخدم مطلوب ولا يتجاوز 50 حرف.");
            When(x => !string.IsNullOrWhiteSpace(x.Email), () =>
            {
                RuleFor(x => x.Email).EmailAddress().WithMessage("البريد الإلكتروني بصيغة غير صحيحة.");
            });
            RuleFor(x => x.Password).NotEmpty().MinimumLength(3).WithMessage("كلمة المرور مطلوبة ولا تقل عن 3 أحرف أو أرقام.");
            RuleFor(x => x.Role).NotEmpty().Must(r => AllowedRoles.Contains(r))
                .WithMessage("الدور يجب أن يكون Admin أو Manager أو Cashier.");
        }
    }
}
