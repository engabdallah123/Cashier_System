using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Settings.Domain.StoreSettings.Entities;

namespace Settings.Infrastructre.Database;

public static class SettingsDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SettingsDbContext>();

        try
        {
            await context.Database.MigrateAsync();
        }
        catch { }

        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Settings].[StoreSettings]') AND name = 'AutoPrintInvoice')
                BEGIN
                    ALTER TABLE [Settings].[StoreSettings] ADD [AutoPrintInvoice] BIT NOT NULL CONSTRAINT DF_StoreSettings_AutoPrintInvoice DEFAULT 1;
                END

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Settings].[StoreSettings]') AND name = 'EnableDepletedBatchAlert')
                BEGIN
                    ALTER TABLE [Settings].[StoreSettings] ADD [EnableDepletedBatchAlert] BIT NOT NULL CONSTRAINT DF_StoreSettings_EnableDepletedBatchAlert DEFAULT 1;
                END

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Settings].[StoreSettings]') AND name = 'LogoUrl')
                BEGIN
                    ALTER TABLE [Settings].[StoreSettings] ADD [LogoUrl] NVARCHAR(500) NULL;
                END");
        }
        catch { }

        if (!await context.StoreSettings.AnyAsync())
        {
            var defaultSetting = StoreSetting.Create(
                storeName: "المتجر الرئيسي",
                currency: "EGP",
                taxRate: 0,
                isTaxIncluded: true,
                address: "القاهرة، مصر",
                phone: null,
                invoiceFooterMessage: "شكراً لزيارتكم!",
                allowNegativeStock: false,
                autoPrintInvoice: true,
                logoUrl: null,
                enableDepletedBatchAlert: true);

            if (defaultSetting.IsSuccess)
            {
                await context.StoreSettings.AddAsync(defaultSetting.Value);
                await context.SaveChangesAsync();
            }
        }
    }
}
