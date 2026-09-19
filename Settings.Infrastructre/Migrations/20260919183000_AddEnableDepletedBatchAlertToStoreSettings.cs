using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Settings.Infrastructre.Migrations
{
    /// <inheritdoc />
    public partial class AddEnableDepletedBatchAlertToStoreSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID(N'[Settings].[StoreSettings]') 
                    AND name = 'EnableDepletedBatchAlert'
                )
                BEGIN
                    ALTER TABLE [Settings].[StoreSettings] ADD [EnableDepletedBatchAlert] bit NOT NULL DEFAULT CAST(1 AS bit);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableDepletedBatchAlert",
                schema: "Settings",
                table: "StoreSettings");
        }
    }
}
