using FluentValidation;

namespace Inventory.Application.Catalog.Products.Commands.CreateProduct
{
    internal sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
    {
        public CreateProductCommandValidator()
        {
            RuleFor(x => x.Barcode).NotEmpty().WithMessage("الباركود مطلوب.");
            RuleFor(x => x)
                .Must(x => !string.IsNullOrWhiteSpace(x.NameAr) || !string.IsNullOrWhiteSpace(x.NameEn))
                .WithMessage("يجب إدخال اسم المنتج باللغة العربية أو الإنجليزية على الأقل.");
            RuleFor(x => x.CategoryId).NotEmpty().WithMessage("معرف التصنيف مطلوب.");
            RuleFor(x => x.UnitId).NotEmpty().WithMessage("معرف الوحدة مطلوب.");
            RuleFor(x => x.PurchasePrice).GreaterThanOrEqualTo(0).WithMessage("سعر الشراء لا يمكن أن يكون سالباً.");
            RuleFor(x => x.SellingPrice).GreaterThanOrEqualTo(0).WithMessage("سعر البيع لا يمكن أن يكون سالباً.");
        }
    }
}
