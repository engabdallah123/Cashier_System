using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Infrastructre.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchesWasteAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseUnit",
                schema: "Inventory",
                table: "Products",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "قطعة");

            migrationBuilder.AddColumn<int>(
                name: "ConversionFactor",
                schema: "Inventory",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ExpiryAlertDays",
                schema: "Inventory",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<string>(
                name: "ParentUnit",
                schema: "Inventory",
                table: "Products",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShelfLifeDays",
                schema: "Inventory",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ExpiryNotifications",
                schema: "Inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RemainingQuantityAtCreation = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DaysRemainingAtCreation = table.Column<int>(type: "int", nullable: false),
                    SnoozedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedWasteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpiryNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryBatches",
                schema: "Inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurchaseInvoiceItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OriginalQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    OriginalUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryWastes",
                schema: "Inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseInvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurchaseInvoiceItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RelatedNotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryWastes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpiryNotifications_BatchId",
                schema: "Inventory",
                table: "ExpiryNotifications",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpiryNotifications_ProductId",
                schema: "Inventory",
                table: "ExpiryNotifications",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpiryNotifications_SnoozedUntil",
                schema: "Inventory",
                table: "ExpiryNotifications",
                column: "SnoozedUntil");

            migrationBuilder.CreateIndex(
                name: "IX_ExpiryNotifications_Status",
                schema: "Inventory",
                table: "ExpiryNotifications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatches_ExpiryDate",
                schema: "Inventory",
                table: "InventoryBatches",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatches_ProductId",
                schema: "Inventory",
                table: "InventoryBatches",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatches_PurchaseInvoiceId",
                schema: "Inventory",
                table: "InventoryBatches",
                column: "PurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatches_PurchaseInvoiceItemId",
                schema: "Inventory",
                table: "InventoryBatches",
                column: "PurchaseInvoiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatches_RemainingQuantity",
                schema: "Inventory",
                table: "InventoryBatches",
                column: "RemainingQuantity");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBatches_Status",
                schema: "Inventory",
                table: "InventoryBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryWastes_CreatedAt",
                schema: "Inventory",
                table: "InventoryWastes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryWastes_InventoryBatchId",
                schema: "Inventory",
                table: "InventoryWastes",
                column: "InventoryBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryWastes_ProductId",
                schema: "Inventory",
                table: "InventoryWastes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryWastes_PurchaseInvoiceId",
                schema: "Inventory",
                table: "InventoryWastes",
                column: "PurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryWastes_Reason",
                schema: "Inventory",
                table: "InventoryWastes",
                column: "Reason");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryWastes_Source",
                schema: "Inventory",
                table: "InventoryWastes",
                column: "Source");

            // Backfill existing products' BaseUnit, ParentUnit, ConversionFactor, and ShelfLife
            migrationBuilder.Sql(@"
                UPDATE [Inventory].[Products]
                SET BaseUnit = N'قطعة',
                    ParentUnit = N'كرتونة',
                    ShelfLifeDays = 30,
                    ExpiryAlertDays = 3;

                UPDATE [Inventory].[Products] SET ConversionFactor = 240 WHERE Id = '3782F50A-8458-4308-9FF6-19803821BD5A';
                UPDATE [Inventory].[Products] SET ConversionFactor = 240 WHERE Id = '91126165-F8AD-403A-A0FC-504518351133';
                UPDATE [Inventory].[Products] SET ConversionFactor = 264 WHERE Id = 'EBEF3F20-719E-4E05-8B9C-5BA20FED0C78';
                UPDATE [Inventory].[Products] SET ConversionFactor = 12  WHERE Id = 'BFD53A0C-ED99-4F83-9047-CDDC53A2C765';
                UPDATE [Inventory].[Products] SET ConversionFactor = 120 WHERE Id = '4E6618DC-34B0-4E26-80B4-F04A4DDAFACB';
                UPDATE [Inventory].[Products] SET ConversionFactor = 440 WHERE Id = '9B9A1D23-36F3-4261-AA38-F76C548C0F9E';

                -- Clean packaging tags from product descriptions
                UPDATE [Inventory].[Products]
                SET Description = LTRIM(RTRIM(REPLACE(Description, SUBSTRING(Description, CHARINDEX('[UNITS:', Description), CHARINDEX(']', Description, CHARINDEX('[UNITS:', Description)) - CHARINDEX('[UNITS:', Description) + 1), '')))
                WHERE Description LIKE '%[UNITS:%]%';

                -- Backfill Inventory Batches for existing stock
                INSERT INTO [Inventory].[InventoryBatches]
                (Id, ProductId, PurchaseInvoiceId, PurchaseInvoiceItemId, BatchNumber, OriginalQuantity, OriginalUnit, BaseQuantity, RemainingQuantity, UnitCost, PurchaseDate, ExpiryDate, Status, CreatedAt)
                VALUES
                ('DDF50EFA-4FD8-4F6E-B9CD-4377B5F4D482', '3782F50A-8458-4308-9FF6-19803821BD5A', '2C19BEC7-A4A6-4741-BC2A-71501D56297A', 'DDF50EFA-4FD8-4F6E-B9CD-4377B5F4D482', '30301020', 20, N'قطعة', 20, 20, 12.50, '2026-09-07 08:56:52', '2026-09-17 00:00:00', 1, '2026-09-07 08:56:52'),
                (NEWID(), '3782F50A-8458-4308-9FF6-19803821BD5A', NULL, NULL, 'BATCH-INIT-1', 13, N'قطعة', 13, 13, 12.50, '2026-08-16 00:00:00', DATEADD(day, 60, GETDATE()), 1, '2026-08-16 00:00:00'),
                ('ABECC046-432A-47B6-8B5D-4F4FC40149BD', '91126165-F8AD-403A-A0FC-504518351133', 'B5E1D55E-46F3-451D-AE95-A0DA396F7AFD', 'ABECC046-432A-47B6-8B5D-4F4FC40149BD', '012010011', 240, N'قطعة', 240, 200, 12.50, '2026-09-07 09:32:09', '2026-09-08 00:00:00', 1, '2026-09-07 09:32:09'),
                ('3211D2E4-C2FF-4954-954A-6603A1A1288C', 'EBEF3F20-719E-4E05-8B9C-5BA20FED0C78', 'D7E2373F-CBA9-4450-9269-FD906CCFCF70', '3211D2E4-C2FF-4954-954A-6603A1A1288C', 'BATCH-PUR-1', 23, N'قطعة', 23, 15, 20.00, '2026-08-31 01:11:10', DATEADD(day, 30, GETDATE()), 1, '2026-08-31 01:11:10'),
                ('7E3FA798-1F8A-4504-A5A3-626154F0658F', '4E6618DC-34B0-4E26-80B4-F04A4DDAFACB', '2C19BEC7-A4A6-4741-BC2A-71501D56297A', '7E3FA798-1F8A-4504-A5A3-626154F0658F', '493059', 60, N'قطعة', 60, 32, 0.69, '2026-09-07 08:56:52', '2026-09-23 00:00:00', 1, '2026-09-07 08:56:52'),
                ('97547D03-7343-4113-BA0F-7E59683F145B', 'BFD53A0C-ED99-4F83-9047-CDDC53A2C765', '76438775-5161-4C84-B645-7808C2028D49', '97547D03-7343-4113-BA0F-7E59683F145B', 'BATCH-PUR-2', 12, N'قطعة', 12, 12, 50.00, '2026-09-07 09:38:04', '2026-09-06 00:00:00', 1, '2026-09-07 09:38:04'),
                (NEWID(), 'BFD53A0C-ED99-4F83-9047-CDDC53A2C765', NULL, NULL, 'BATCH-INIT-2', 22, N'قطعة', 22, 22, 50.00, '2026-08-16 00:00:00', DATEADD(day, 45, GETDATE()), 1, '2026-08-16 00:00:00'),
                (NEWID(), '9B9A1D23-36F3-4261-AA38-F76C548C0F9E', NULL, NULL, 'BATCH-INIT-3', 7, N'قطعة', 7, 7, 9.09, '2026-08-16 00:00:00', DATEADD(day, 60, GETDATE()), 1, '2026-08-16 00:00:00');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpiryNotifications",
                schema: "Inventory");

            migrationBuilder.DropTable(
                name: "InventoryBatches",
                schema: "Inventory");

            migrationBuilder.DropTable(
                name: "InventoryWastes",
                schema: "Inventory");

            migrationBuilder.DropColumn(
                name: "BaseUnit",
                schema: "Inventory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ConversionFactor",
                schema: "Inventory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ExpiryAlertDays",
                schema: "Inventory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ParentUnit",
                schema: "Inventory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ShelfLifeDays",
                schema: "Inventory",
                table: "Products");
        }
    }
}
