using Inventory.Domain;
using Inventory.Domain.Batches.Entities;
using Inventory.Domain.Catalog.Products.Entities;
using Inventory.Domain.Catalog.Products.Errors;
using Inventory.Domain.Stock.StockMovements;
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
                request.TaxRate, request.ImageUrl, request.Id);

            if (productResult.IsFailure)
                return Result<Guid>.Failure(productResult.Error);

            var product = productResult.Value!;

            if (request.InitialStock > 0)
            {
                product.AdjustStock(request.InitialStock, allowNegativeStock: true);

                var batchResult = InventoryBatch.Create(
                    productId: product.Id,
                    originalQuantity: request.InitialStock,
                    originalUnit: product.BaseUnit,
                    baseQuantity: request.InitialStock,
                    unitCost: product.PurchasePrice,
                    purchaseDate: DateTime.UtcNow,
                    expiryDate: product.TrackExpiry && product.ShelfLifeDays > 0 ? DateTime.UtcNow.AddDays(product.ShelfLifeDays) : null,
                    batchNumber: $"OPENING-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}"
                );

                if (batchResult.IsSuccess)
                {
                    await _unitOfWork.BatchRepository.AddAsync(batchResult.Value!, cancellationToken);
                }

                var userId = request.CreatedBy.HasValue && request.CreatedBy.Value != Guid.Empty
                    ? request.CreatedBy.Value
                    : Guid.Parse("00000000-0000-0000-0000-000000000001");

                var movementResult = StockMovement.Create(
                    product.Id,
                    request.InitialStock,
                    StockMovementType.Adjustment,
                    userId,
                    reference: "رصيد افتتاحي",
                    notes: $"تسجيل رصيد افتتاحي للمنتج: {request.InitialStock} {product.BaseUnit}"
                );

                if (movementResult.IsSuccess)
                {
                    await _unitOfWork.StockMovementRepository.AddAsync(movementResult.Value!);
                }
            }

            await _unitOfWork.ProductRepository.AddAsync(product, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _cacheService.RemoveByPrefixAsync("products_", cancellationToken);
            await _cacheService.RemoveByPrefixAsync("dashboard_", cancellationToken);

            return Result<Guid>.Success(product.Id);
        }
    }
}
