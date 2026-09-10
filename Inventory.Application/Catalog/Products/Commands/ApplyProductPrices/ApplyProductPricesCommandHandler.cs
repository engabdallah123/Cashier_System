using Inventory.Domain;
using POS.Shared.Application.IService;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Inventory.Application.Catalog.Products.Commands.ApplyProductPrices
{
    internal sealed class ApplyProductPricesCommandHandler : ICommandHandler<ApplyProductPricesCommand>
    {
        private readonly IInventoryUnitOfWork _unitOfWork;
        private readonly ICacheService _cacheService;

        public ApplyProductPricesCommandHandler(
            IInventoryUnitOfWork unitOfWork,
            ICacheService cacheService)
        {
            _unitOfWork = unitOfWork;
            _cacheService = cacheService;
        }

        public async Task<Result> Handle(ApplyProductPricesCommand request, CancellationToken cancellationToken)
        {
            var product = await _unitOfWork.ProductRepository.GetByIdAsync(request.ProductId, cancellationToken);
            if (product is null)
                return Result.Failure(new Error("Product.NotFound", "المنتج غير موجود."));

            var applyResult = product.ApplyPrices(request.CostPrice, request.SellingPrice, request.WholesalePrice);
            if (applyResult.IsFailure)
                return applyResult;

            _unitOfWork.ProductRepository.Update(product);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _cacheService.RemoveByPrefixAsync("products_", cancellationToken);
            await _cacheService.RemoveByPrefixAsync("dashboard_", cancellationToken);

            return Result.Success();
        }
    }
}
