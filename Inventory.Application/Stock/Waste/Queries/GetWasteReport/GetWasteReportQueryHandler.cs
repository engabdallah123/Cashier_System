using Inventory.Domain;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Inventory.Application.Stock.Waste.Queries.GetWasteReport
{
    internal sealed class GetWasteReportQueryHandler : IQueryHandler<GetWasteReportQuery, WasteReportResponse>
    {
        private readonly IInventoryUnitOfWork _unitOfWork;

        public GetWasteReportQueryHandler(IInventoryUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<WasteReportResponse>> Handle(GetWasteReportQuery request, CancellationToken cancellationToken)
        {
            var wastes = await _unitOfWork.WasteRepository.GetFilteredAsync(
                fromDate: request.FromDate,
                toDate: request.ToDate,
                productId: request.ProductId,
                reason: request.Reason,
                source: request.Source,
                ct: cancellationToken);

            var items = new List<WasteItemDto>();
            decimal totalLoss = 0;

            foreach (var w in wastes)
            {
                var product = await _unitOfWork.ProductRepository.GetByIdAsync(w.ProductId, cancellationToken);
                var batch = await _unitOfWork.BatchRepository.GetByIdAsync(w.InventoryBatchId, cancellationToken);

                items.Add(new WasteItemDto(
                    w.Id,
                    w.ProductId,
                    product?.NameAr ?? "منتج غير معروف",
                    product?.Barcode ?? "-",
                    w.InventoryBatchId,
                    batch?.BatchNumber ?? "-",
                    w.Quantity,
                    w.Unit,
                    w.BaseQuantity,
                    w.UnitCost,
                    w.TotalCost,
                    w.Reason,
                    w.Source,
                    w.Notes,
                    w.CreatedAt));

                totalLoss += w.TotalCost;
            }

            return Result<WasteReportResponse>.Success(new WasteReportResponse(
                items,
                Math.Round(totalLoss, 2),
                items.Count));
        }
    }
}
