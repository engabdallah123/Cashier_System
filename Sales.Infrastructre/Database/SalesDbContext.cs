using MediatR;
using POS.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using POS.Shared.Application.Exceptions;
using POS.Shared.Infrastructure.Database;
using System.Reflection;
using Sales.Domain.Customers.Entities;
using Sales.Domain.Sales.Entities;

namespace Sales.Infrastructre.Database;

public class SalesDbContext : DbContext
{
    private readonly IMediator _mediator;

    public SalesDbContext(DbContextOptions<SalesDbContext> options, IMediator mediator)
        : base(options)
    {
        _mediator = mediator;
    }

    public DbSet<Customer> Customers { get; set; }
    public DbSet<Sale> Sales { get; set; }
    public DbSet<SaleItem> SaleItems { get; set; }
    public DbSet<SalePayment> SalePayments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schemas.Sales);
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
