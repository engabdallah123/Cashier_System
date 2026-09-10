using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Shared.Infrastructure.Database;
using Sales.Domain.Sales.Entities;

namespace Sales.Infrastructre.Configurations;

internal sealed class SalePaymentConfiguration : IEntityTypeConfiguration<SalePayment>
{
    public void Configure(EntityTypeBuilder<SalePayment> builder)
    {
        builder.ToTable("SalePayments", Schemas.Sales);

        builder.HasKey(p => p.Id);

        builder.Property(p => p.SaleId).IsRequired();
        builder.Property(p => p.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(p => p.PaymentDate).IsRequired();

        builder.Property(p => p.PaymentMethod)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.CashierId).IsRequired();
        builder.Property(p => p.ShiftId);
        builder.Property(p => p.Notes).HasMaxLength(500);

        builder.HasIndex(p => p.SaleId);
        builder.HasIndex(p => p.PaymentDate);
        builder.HasIndex(p => p.CashierId);
        builder.HasIndex(p => p.ShiftId);
    }
}
