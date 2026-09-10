using Inventory.Domain.Batches.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Shared.Infrastructure.Database;

namespace Inventory.Infrastructre.Configurations
{
    internal sealed class InventoryBatchConfiguration : IEntityTypeConfiguration<InventoryBatch>
    {
        public void Configure(EntityTypeBuilder<InventoryBatch> builder)
        {
            builder.ToTable("InventoryBatches", Schemas.Inventory);

            builder.HasKey(b => b.Id);

            builder.Property(b => b.BatchNumber)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(b => b.OriginalQuantity)
                .HasPrecision(18, 3)
                .IsRequired();

            builder.Property(b => b.OriginalUnit)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(b => b.BaseQuantity)
                .HasPrecision(18, 3)
                .IsRequired();

            builder.Property(b => b.RemainingQuantity)
                .HasPrecision(18, 3)
                .IsRequired();

            builder.Property(b => b.UnitCost)
                .HasPrecision(18, 4)
                .IsRequired();

            builder.Property(b => b.Status)
                .IsRequired();

            builder.Property(b => b.PurchaseDate)
                .IsRequired();

            builder.Property(b => b.ExpiryDate);

            builder.Property(b => b.CreatedAt)
                .IsRequired();

            builder.HasIndex(b => b.ProductId);
            builder.HasIndex(b => b.PurchaseInvoiceId);
            builder.HasIndex(b => b.PurchaseInvoiceItemId);
            builder.HasIndex(b => b.ExpiryDate);
            builder.HasIndex(b => b.RemainingQuantity);
            builder.HasIndex(b => b.Status);
        }
    }
}
