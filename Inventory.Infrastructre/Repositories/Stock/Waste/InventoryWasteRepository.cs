using Inventory.Domain.Stock.Waste.Entities;
using Inventory.Domain.Stock.Waste.Interface;
using Inventory.Infrastructre.Database;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructre.Repositories.Stock.Waste
{
    public class InventoryWasteRepository : IInventoryWasteRepository
    {
        private readonly InventoryDbContext _context;

        public InventoryWasteRepository(InventoryDbContext context)
        {
            _context = context;
        }

        public async Task<InventoryWaste?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _context.Wastes.FirstOrDefaultAsync(w => w.Id == id, ct);
        }

        public async Task<IReadOnlyList<InventoryWaste>> GetFilteredAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            Guid? productId = null,
            string? reason = null,
            string? source = null,
            CancellationToken ct = default)
        {
            var query = _context.Wastes.AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(w => w.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(w => w.CreatedAt <= toDate.Value);

            if (productId.HasValue && productId.Value != Guid.Empty)
                query = query.Where(w => w.ProductId == productId.Value);

            if (!string.IsNullOrWhiteSpace(reason) && reason != "all")
                query = query.Where(w => w.Reason == reason.Trim());

            if (!string.IsNullOrWhiteSpace(source) && source != "all")
                query = query.Where(w => w.Source == source.Trim());

            return await query.OrderByDescending(w => w.CreatedAt).ToListAsync(ct);
        }

        public async Task<decimal> GetTotalLossAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default)
        {
            var query = _context.Wastes.AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(w => w.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(w => w.CreatedAt <= toDate.Value);

            return await query.SumAsync(w => w.TotalCost, ct);
        }

        public async Task<int> GetCountAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default)
        {
            var query = _context.Wastes.AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(w => w.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(w => w.CreatedAt <= toDate.Value);

            return await query.CountAsync(ct);
        }

        public async Task AddAsync(InventoryWaste waste, CancellationToken ct = default)
        {
            await _context.Wastes.AddAsync(waste, ct);
        }
    }
}
