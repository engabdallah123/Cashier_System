using Inventory.Domain;
using Inventory.Domain.Batches.Interface;
using Inventory.Domain.Catalog.Categories;
using Inventory.Domain.Catalog.Products.Interface;
using Inventory.Domain.Catalog.Units;
using Inventory.Domain.Notifications.Interface;
using Inventory.Domain.Stock.StockMovements;
using Inventory.Domain.Stock.Waste.Interface;
using Inventory.Infrastructre.Database;
using Inventory.Infrastructre.Repositories.Batches;
using Inventory.Infrastructre.Repositories.Catalog;
using Inventory.Infrastructre.Repositories.Notifications;
using Inventory.Infrastructre.Repositories.Stock.Waste;
using POS.Shared.Domain;
using POS.Shared.Infrastructure.Database;

namespace Inventory.Infrastructre
{
    public class InventoryUnitOfWork : IInventoryUnitOfWork
    {
        private readonly InventoryDbContext _dbContext;

        public IProductRepository ProductRepository { get; private set; }
        public IBaseRepository<Category> CategoryRepository { get; private set; }
        public IBaseRepository<Unit> UnitRepository { get; private set; }
        public IBaseRepository<StockMovement> StockMovementRepository { get; private set; }
        public IInventoryBatchRepository BatchRepository { get; private set; }
        public IInventoryWasteRepository WasteRepository { get; private set; }
        public IExpiryNotificationRepository NotificationRepository { get; private set; }

        public InventoryUnitOfWork(InventoryDbContext dbContext)
        {
            _dbContext = dbContext;
            ProductRepository = new ProductRepository(_dbContext);
            CategoryRepository = new BaseRepository<Category>(_dbContext);
            UnitRepository = new BaseRepository<Unit>(_dbContext);
            StockMovementRepository = new BaseRepository<StockMovement>(_dbContext);
            BatchRepository = new InventoryBatchRepository(_dbContext);
            WasteRepository = new InventoryWasteRepository(_dbContext);
            NotificationRepository = new ExpiryNotificationRepository(_dbContext);
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
