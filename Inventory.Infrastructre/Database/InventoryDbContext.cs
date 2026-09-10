using Inventory.Domain.Batches.Entities;
using Inventory.Domain.Catalog.Categories;
using Inventory.Domain.Catalog.Products.Entities;
using Inventory.Domain.Catalog.Units;
using Inventory.Domain.Notifications.Entities;
using Inventory.Domain.Stock.StockMovements;
using Inventory.Domain.Stock.Waste.Entities;
using MediatR;
using Unit = Inventory.Domain.Catalog.Units.Unit;
using Microsoft.EntityFrameworkCore;
using POS.Shared.Application.Exceptions;
using POS.Shared.Domain;
using POS.Shared.Infrastructure.Database;
using System.Reflection;

namespace Inventory.Infrastructre.Database;

public class InventoryDbContext : DbContext
{
    private readonly IMediator _mediator;

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options, IMediator mediator)
        : base(options)
    {
        _mediator = mediator;
    }

    public DbSet<Product> Products { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Unit> Units { get; set; }
    public DbSet<StockMovement> StockMovements { get; set; }
    public DbSet<InventoryBatch> Batches { get; set; }
    public DbSet<InventoryWaste> Wastes { get; set; }
    public DbSet<ExpiryNotification> Notifications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schemas.Inventory);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var domainEvents = ChangeTracker
                .Entries<Entity>()
                .Select(e => e.Entity)
                .Where(e => e.GetDomainEvents().Any())
                .SelectMany(e =>
                {
                    var events = e.GetDomainEvents().ToList();
                    e.ClearDomainEvents();
                    return events;
                })
                .ToList();

            var result = await base.SaveChangesAsync(cancellationToken);

            foreach (var domainEvent in domainEvents)
                await _mediator.Publish(domainEvent, cancellationToken);

            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.FirstOrDefault();
            var entityName = entry?.Metadata.ClrType.Name ?? "Unknown Entity";
            var state = entry?.State.ToString() ?? "Unknown State";

            throw new ConcurrencyException(
                $"Concurrency error on Entity: '{entityName}' | State: '{state}'.", ex);
        }
    }
}
