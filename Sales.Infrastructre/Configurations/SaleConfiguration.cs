using Sales.Domain.Sales.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Shared.Infrastructure.Database;

namespace Sales.Infrastructre.Configurations;

internal sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales", Schemas.Sales);

        builder.HasKey(s => s.Id);

        builder.Property(s => s.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(s => s.InvoiceNumber).IsUnique();

        builder.Property(s => s.SaleDate).IsRequired();
        builder.Property(s => s.CashierId).IsRequired();
        builder.Property(s => s.ShiftId).IsRequired();
        builder.Property(s => s.CustomerId);

        builder.Property(s => s.SubTotal).HasPrecision(18, 2);
        builder.Property(s => s.DiscountAmount).HasPrecision(18, 2);
        builder.Property(s => s.TaxAmount).HasPrecision(18, 2);
        builder.Property(s => s.TotalAmount).HasPrecision(18, 2);
        builder.Property(s => s.PaidAmount).HasPrecision(18, 2);
        builder.Property(s => s.ChangeAmount).HasPrecision(18, 2);

        builder.Property(s => s.PaymentMethod)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.Status).IsRequired();
        builder.Property(s => s.Notes).HasMaxLength(500);

        builder.HasMany(s => s.Items)
            .WithOne()
            .HasForeignKey(i => i.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Payments)
            .WithOne()
            .HasForeignKey(p => p.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.CashierId);
        builder.HasIndex(s => s.ShiftId);
        builder.HasIndex(s => s.CustomerId);
        builder.HasIndex(s => s.SaleDate);
    }
}
