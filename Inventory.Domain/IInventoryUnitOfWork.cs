using Inventory.Domain.Batches.Interface;
using Inventory.Domain.Catalog.Categories;
using Inventory.Domain.Catalog.Products.Entities;
using Inventory.Domain.Catalog.Products.Interface;
using Inventory.Domain.Catalog.Units;
using Inventory.Domain.Notifications.Interface;
using Inventory.Domain.Stock.StockMovements;
using Inventory.Domain.Stock.Waste.Interface;
using POS.Shared.Domain;
using POS.Shared.Domain.Abstractions;

namespace Inventory.Domain
{
    public interface IInventoryUnitOfWork : IUnitOfWork
    {
        IProductRepository ProductRepository { get; }
        IBaseRepository<Category> CategoryRepository { get; }
        IBaseRepository<Unit> UnitRepository { get; }
        IBaseRepository<StockMovement> StockMovementRepository { get; }
        IInventoryBatchRepository BatchRepository { get; }
        IInventoryWasteRepository WasteRepository { get; }
        IExpiryNotificationRepository NotificationRepository { get; }
    }
}