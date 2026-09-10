using POS.Shared.Application.Messaging;

namespace Inventory.Application.Catalog.Products.Commands.UpdateProduct
{
    public sealed record UpdateProductCommand(
        Guid Id,
        string Barcode,
        string NameAr,
        string NameEn,
        string? Description,
        Guid CategoryId,
        Guid UnitId,
        Guid? SupplierId,
        string BaseUnit,
        string? ParentUnit,
        int ConversionFactor,
        int ShelfLifeDays,
        int ExpiryAlertDays,
        decimal PurchasePrice,
        decimal SellingPrice,
        decimal WholesalePrice,
        decimal ReorderLevel,
        decimal MaxStockLevel,
        bool IsWeighable,
        bool IsActive,
        bool TrackExpiry,
        decimal TaxRate,
        string? ImageUrl) : ICommand;
}
