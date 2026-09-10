using Settings.Domain.StoreSettings.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Shared.Infrastructure.Database;

namespace Settings.Infrastructre.Configurations;

internal sealed class StoreSettingConfiguration : IEntityTypeConfiguration<StoreSetting>
{
    public void Configure(EntityTypeBuilder<StoreSetting> builder)
    {
        builder.ToTable("StoreSettings", Schemas.Settings);

        builder.HasKey(s => s.Id);

        builder.Property(s => s.StoreName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Address)
            .HasMaxLength(500);

        builder.Property(s => s.Phone)
            .HasMaxLength(20);

        builder.Property(s => s.TaxRate)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(s => s.IsTaxIncluded)
            .IsRequired();

        builder.Property(s => s.Currency)
            .IsRequired()
            .HasMaxLength(5);

        builder.Property(s => s.InvoiceFooterMessage)
            .HasMaxLength(500);

        builder.Property(s => s.AllowNegativeStock)
            .IsRequired();

        builder.Property(s => s.AutoPrintInvoice)
            .HasDefaultValue(true);

        builder.Property(s => s.LogoUrl)
            .HasMaxLength(500);

        builder.Property(s => s.UpdatedAt)
            .IsRequired();
    }
}
