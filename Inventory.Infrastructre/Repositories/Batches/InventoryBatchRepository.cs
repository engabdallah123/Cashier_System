using Inventory.Domain.Batches.Entities;
using Inventory.Domain.Batches.Interface;
using Inventory.Infrastructre.Database;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructre.Repositories.Batches
{
    public class InventoryBatchRepository : IInventoryBatchRepository
    {
        private readonly InventoryDbContext _context;

        public InventoryBatchRepository(InventoryDbContext context)
        {
            _context = context;
        }

        public async Task<InventoryBatch?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _context.Batches.FirstOrDefaultAsync(b => b.Id == id, ct);
        }

        public async Task<IReadOnlyList<InventoryBatch>> GetActiveBatchesByProductIdAsync(Guid productId, CancellationToken ct = default)
        {
            return await _context.Batches
                .Where(b => b.ProductId == productId && b.RemainingQuantity > 0 && b.Status == BatchStatus.Active)
                .OrderBy(b => b.PurchaseDate)
                .ThenBy(b => b.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<InventoryBatch>> GetAllActiveBatchesAsync(CancellationToken ct = default)
        {
            return await _context.Batches
                .Where(b => b.RemainingQuantity > 0 && b.Status == BatchStatus.Active)
                .OrderBy(b => b.PurchaseDate)
                .ThenBy(b => b.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<InventoryBatch?> GetByPurchaseItemIdAsync(Guid purchaseItemId, CancellationToken ct = default)
        {
            return await _context.Batches
                .FirstOrDefaultAsync(b => b.PurchaseInvoiceItemId == purchaseItemId, ct);
        }

        public async Task<IReadOnlyList<InventoryBatch>> GetBatchesByPurchaseIdAsync(Guid purchaseId, CancellationToken ct = default)
        {
            return await _context.Batches
                .Where(b => b.PurchaseInvoiceId == purchaseId)
                .ToListAsync(ct);
        }

        public async Task AddAsync(InventoryBatch batch, CancellationToken ct = default)
        {
            await _context.Batches.AddAsync(batch, ct);
        }

        public void Update(InventoryBatch batch)
        {
            _context.Batches.Update(batch);
        }

        public void Delete(InventoryBatch batch)
        {
            _context.Batches.Remove(batch);
        }
    }
}
