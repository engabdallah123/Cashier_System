using FluentValidation;

namespace Inventory.Application.Catalog.Products.Commands.ApplyProductPrices
{
    internal sealed class ApplyProductPricesCommandValidator : AbstractValidator<ApplyProductPricesCommand>
    {
        public ApplyProductPricesCommandValidator()
        {
            RuleFor(x => x.ProductId).NotEmpty().WithMessage("يجب تحديد المنتج.");
            RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0).WithMessage("سعر التكلفة لا يمكن أن يكون سالبًا.");
            RuleFor(x => x.SellingPrice).GreaterThanOrEqualTo(0).WithMessage("سعر البيع قطاعي لا يمكن أن يكون سالبًا.");
            RuleFor(x => x.WholesalePrice).GreaterThanOrEqualTo(0).WithMessage("سعر البيع جملة لا يمكن أن يكون سالبًا.");
        }
    }
}
