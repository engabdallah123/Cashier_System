using Inventory.Domain;
using Inventory.Domain.Catalog.Products.Entities;
using Inventory.Domain.Catalog.Products.Errors;
using POS.Shared.Application.IService;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Inventory.Application.Catalog.Products.Commands.CreateProduct
{
    internal sealed class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, Guid>
    {
        private readonly IInventoryUnitOfWork _unitOfWork;
        private readonly ICacheService _cacheService;

        public CreateProductCommandHandler(
            IInventoryUnitOfWork unitOfWork,
            ICacheService cacheService)
        {
            _unitOfWork = unitOfWork;
            _cacheService = cacheService;
        }

        public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        {
            var barcodeExists = await _unitOfWork.ProductRepository
                .BarcodeExistsAsync(request.Barcode.Trim(), cancellationToken);

            if (barcodeExists)
                return Result<Guid>.Failure(ProductErrors.DuplicateBarcode);

            var productResult = Product.Create(
                request.Barcode, request.NameAr, request.NameEn,
                request.CategoryId, request.UnitId,
                request.PurchasePrice, request.SellingPrice, request.WholesalePrice,
                request.SupplierId, request.Description,
                request.BaseUnit, request.ParentUnit, request.ConversionFactor,
                request.ShelfLifeDays, request.ExpiryAlertDays,
                request.ReorderLevel, request.MaxStockLevel,
                request.IsWeighable, request.IsActive, request.TrackExpiry,
                request.TaxRate, request.ImageUrl);

            if (productResult.IsFailure)
                return Result<Guid>.Failure(productResult.Error);

            var product = productResult.Value!;

            await _unitOfWork.ProductRepository.AddAsync(product, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _cacheService.RemoveByPrefixAsync("products_", cancellationToken);
            await _cacheService.RemoveByPrefixAsync("dashboard_", cancellationToken);

            return Result<Guid>.Success(product.Id);
        }
    }
}
