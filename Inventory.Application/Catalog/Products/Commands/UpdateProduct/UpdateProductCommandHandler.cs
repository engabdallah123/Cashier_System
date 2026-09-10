using Inventory.Domain;
using Inventory.Domain.Catalog.Products.Entities;
using Inventory.Domain.Catalog.Products.Errors;
using POS.Shared.Application.IService;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Inventory.Application.Catalog.Products.Commands.UpdateProduct
{
    internal sealed class UpdateProductCommandHandler : ICommandHandler<UpdateProductCommand>
    {
        private readonly IInventoryUnitOfWork _unitOfWork;
        private readonly ICacheService _cacheService;

        public UpdateProductCommandHandler(
            IInventoryUnitOfWork unitOfWork,
            ICacheService cacheService)
        {
            _unitOfWork = unitOfWork;
            _cacheService = cacheService;
        }

        public async Task<Result> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
        {
            var product = await _unitOfWork.ProductRepository.GetByIdAsync(request.Id, cancellationToken);
            if (product is null)
                return Result.Failure(ProductErrors.NotFound(request.Id));

            var imageUrlToSet = request.ImageUrl switch
            {
                "__CLEAR__" or "__REMOVE__" or "REMOVE" => null,
                not null and not "" => request.ImageUrl,
                _ => product.ImageUrl
            };

            var updateResult = product.Update(
                request.Barcode, request.NameAr, request.NameEn, request.Description,
                request.CategoryId, request.UnitId, request.SupplierId,
                request.BaseUnit, request.ParentUnit, request.ConversionFactor,
                request.ShelfLifeDays, request.ExpiryAlertDays,
                request.PurchasePrice, request.SellingPrice, request.WholesalePrice,
                request.ReorderLevel, request.MaxStockLevel,
                request.IsWeighable, request.IsActive, request.TrackExpiry,
                request.TaxRate, imageUrlToSet);

            if (updateResult.IsFailure)
                return updateResult;

            _unitOfWork.ProductRepository.Update(product);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _cacheService.RemoveByPrefixAsync("products_", cancellationToken);
            await _cacheService.RemoveAsync($"product_id_{request.Id}", cancellationToken);
            await _cacheService.RemoveAsync($"product_barcode_{request.Barcode}", cancellationToken);
            await _cacheService.RemoveByPrefixAsync("dashboard_", cancellationToken);

            return Result.Success();
        }
    }
}
