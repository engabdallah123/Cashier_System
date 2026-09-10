using Inventory.Domain.Stock.Waste.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Shared.Infrastructure.Database;

namespace Inventory.Infrastructre.Configurations
{
    internal sealed class InventoryWasteConfiguration : IEntityTypeConfiguration<InventoryWaste>
    {
        public void Configure(EntityTypeBuilder<InventoryWaste> builder)
        {
            builder.ToTable("InventoryWastes", Schemas.Inventory);

            builder.HasKey(w => w.Id);

            builder.Property(w => w.Quantity)
                .HasPrecision(18, 3)
                .IsRequired();

            builder.Property(w => w.Unit)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(w => w.BaseQuantity)
                .HasPrecision(18, 3)
                .IsRequired();

            builder.Property(w => w.UnitCost)
                .HasPrecision(18, 4)
                .IsRequired();

            builder.Property(w => w.TotalCost)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(w => w.Reason)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(w => w.Source)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(w => w.Notes)
                .HasMaxLength(500);

            builder.Property(w => w.CreatedAt)
                .IsRequired();

            builder.HasIndex(w => w.ProductId);
            builder.HasIndex(w => w.InventoryBatchId);
            builder.HasIndex(w => w.PurchaseInvoiceId);
            builder.HasIndex(w => w.CreatedAt);
            builder.HasIndex(w => w.Reason);
            builder.HasIndex(w => w.Source);
        }
    }
}
