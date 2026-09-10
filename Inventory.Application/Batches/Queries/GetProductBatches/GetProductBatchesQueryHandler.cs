using Inventory.Domain;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Inventory.Application.Batches.Queries.GetProductBatches
{
    internal sealed class GetProductBatchesQueryHandler : IQueryHandler<GetProductBatchesQuery, IReadOnlyList<ProductBatchDto>>
    {
        private readonly IInventoryUnitOfWork _unitOfWork;

        public GetProductBatchesQueryHandler(IInventoryUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<IReadOnlyList<ProductBatchDto>>> Handle(GetProductBatchesQuery request, CancellationToken cancellationToken)
        {
            var product = await _unitOfWork.ProductRepository.GetByIdAsync(request.ProductId, cancellationToken);
            if (product is null)
                return Result<IReadOnlyList<ProductBatchDto>>.Failure(new Error("Product.NotFound", "المنتج غير موجود."));

            var batches = await _unitOfWork.BatchRepository.GetActiveBatchesByProductIdAsync(request.ProductId, cancellationToken);

            int factor = product.ConversionFactor > 0 ? product.ConversionFactor : 1;
            var today = DateTime.Today;

            var dtoList = batches.Select(b =>
            {
                int? daysUntilExpiry = b.ExpiryDate.HasValue ? (int)(b.ExpiryDate.Value.Date - today).TotalDays : null;
                decimal remainingInParent = factor > 1 ? Math.Round(b.RemainingQuantity / factor, 2) : b.RemainingQuantity;
                decimal cartonCost = factor > 1 ? Math.Round(b.UnitCost * factor, 2) : b.UnitCost;

                return new ProductBatchDto(
                    b.Id,
                    b.ProductId,
                    b.BatchNumber,
                    b.OriginalQuantity,
                    b.OriginalUnit,
                    b.BaseQuantity,
                    b.RemainingQuantity,
                    remainingInParent,
                    b.UnitCost,
                    cartonCost,
                    b.PurchaseDate,
                    b.ExpiryDate,
                    daysUntilExpiry,
                    b.Status.ToString());
            }).ToList();

            return Result<IReadOnlyList<ProductBatchDto>>.Success(dtoList);
        }
    }
}
