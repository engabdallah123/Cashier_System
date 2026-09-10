using Inventory.Domain.Notifications.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Shared.Infrastructure.Database;

namespace Inventory.Infrastructre.Configurations
{
    internal sealed class ExpiryNotificationConfiguration : IEntityTypeConfiguration<ExpiryNotification>
    {
        public void Configure(EntityTypeBuilder<ExpiryNotification> builder)
        {
            builder.ToTable("ExpiryNotifications", Schemas.Inventory);

            builder.HasKey(n => n.Id);

            builder.Property(n => n.Type)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(n => n.Status)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(n => n.Message)
                .IsRequired()
                .HasMaxLength(1000);

            builder.Property(n => n.RemainingQuantityAtCreation)
                .HasPrecision(18, 3)
                .IsRequired();

            builder.Property(n => n.DaysRemainingAtCreation)
                .IsRequired();

            builder.Property(n => n.CreatedAt)
                .IsRequired();

            builder.HasIndex(n => n.ProductId);
            builder.HasIndex(n => n.BatchId);
            builder.HasIndex(n => n.Status);
            builder.HasIndex(n => n.SnoozedUntil);
        }
    }
}
