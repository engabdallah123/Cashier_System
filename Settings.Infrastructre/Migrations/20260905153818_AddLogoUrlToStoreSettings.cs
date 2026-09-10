using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Settings.Infrastructre.Migrations
{
    /// <inheritdoc />
    public partial class AddLogoUrlToStoreSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID(N'[Settings].[StoreSettings]') 
                    AND name = 'AutoPrintInvoice'
                )
                BEGIN
                    ALTER TABLE [Settings].[StoreSettings] ADD [AutoPrintInvoice] bit NOT NULL DEFAULT CAST(1 AS bit);
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID(N'[Settings].[StoreSettings]') 
                    AND name = 'LogoUrl'
                )
                BEGIN
                    ALTER TABLE [Settings].[StoreSettings] ADD [LogoUrl] nvarchar(500) NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoPrintInvoice",
                schema: "Settings",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                schema: "Settings",
                table: "StoreSettings");
        }
    }
}
