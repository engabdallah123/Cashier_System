using Inventory.Domain.Batches.Entities;

namespace Inventory.Domain.Batches.Interface
{
    public interface IInventoryBatchRepository
    {
        Task<InventoryBatch?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<InventoryBatch>> GetActiveBatchesByProductIdAsync(Guid productId, CancellationToken ct = default);
        Task<IReadOnlyList<InventoryBatch>> GetAllActiveBatchesAsync(CancellationToken ct = default);
        Task<InventoryBatch?> GetByPurchaseItemIdAsync(Guid purchaseItemId, CancellationToken ct = default);
        Task<IReadOnlyList<InventoryBatch>> GetBatchesByPurchaseIdAsync(Guid purchaseId, CancellationToken ct = default);
        Task<int> GetMaxTodayBatchSequenceAsync(DateTime date, CancellationToken ct = default);
        Task AddAsync(InventoryBatch batch, CancellationToken ct = default);
        void Update(InventoryBatch batch);
        void Delete(InventoryBatch batch);
    }
}
