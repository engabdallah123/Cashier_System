using Inventory.Domain.Stock.Waste.Entities;

namespace Inventory.Domain.Stock.Waste.Interface
{
    public interface IInventoryWasteRepository
    {
        Task<InventoryWaste?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<InventoryWaste>> GetFilteredAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            Guid? productId = null,
            string? reason = null,
            string? source = null,
            CancellationToken ct = default);
        Task<decimal> GetTotalLossAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default);
        Task<int> GetCountAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default);
        Task AddAsync(InventoryWaste waste, CancellationToken ct = default);
    }
}
