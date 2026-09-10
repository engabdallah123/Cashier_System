using Inventory.Domain.Catalog.Products.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Shared.Infrastructure.Database;

namespace Inventory.Infrastructre.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", Schemas.Inventory);

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Barcode)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(p => p.Barcode).IsUnique();

        builder.Property(p => p.NameAr)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.NameEn)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.PurchasePrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.SellingPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.WholesalePrice)
            .HasPrecision(18, 2);

        builder.Property(p => p.QuantityInStock)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.Property(p => p.ReorderLevel)
            .HasPrecision(18, 3);

        builder.Property(p => p.MaxStockLevel)
            .HasPrecision(18, 3);

        builder.Property(p => p.TaxRate)
            .HasPrecision(5, 2);

        builder.Property(p => p.ImageUrl)
            .HasMaxLength(500);

        builder.Property(p => p.BaseUnit)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("قطعة");

        builder.Property(p => p.ParentUnit)
            .HasMaxLength(50);

        builder.Property(p => p.ConversionFactor)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(p => p.ShelfLifeDays)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(p => p.ExpiryAlertDays)
            .HasDefaultValue(3)
            .IsRequired();

        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => p.UnitId);
        builder.HasIndex(p => p.SupplierId);
        builder.HasIndex(p => p.IsActive);
    }
}
