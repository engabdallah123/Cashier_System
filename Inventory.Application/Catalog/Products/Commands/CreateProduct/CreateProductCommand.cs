using POS.Shared.Application.Messaging;

namespace Inventory.Application.Catalog.Products.Commands.CreateProduct
{
    public sealed record CreateProductCommand(
        string Barcode,
        string NameAr,
        string NameEn,
        Guid CategoryId,
        Guid UnitId,
        decimal PurchasePrice,
        decimal SellingPrice,
        decimal WholesalePrice = 0,
        Guid? SupplierId = null,
        string? Description = null,
        string BaseUnit = "قطعة",
        string? ParentUnit = "كرتونة",
        int ConversionFactor = 1,
        int ShelfLifeDays = 0,
        int ExpiryAlertDays = 3,
        decimal ReorderLevel = 5,
        decimal MaxStockLevel = 100,
        bool IsWeighable = false,
        bool IsActive = true,
        bool TrackExpiry = false,
        decimal TaxRate = 0,
        string? ImageUrl = null,
        Guid? Id = null,
        decimal InitialStock = 0,
        Guid? CreatedBy = null) : ICommand<Guid>;
}
