namespace Purchases.Application.Purchases.Queries.GetPurchaseById
{
    public sealed record PurchaseDetailItemResponse(
        Guid Id,
        Guid ProductId,
        string? ProductName,
        string? Barcode,
        decimal Quantity,
        decimal UnitCost,
        decimal Discount,
        decimal Tax,
        decimal Total,
        DateTime? ExpiryDate,
        string? BatchNumber,
        decimal ReturnedQuantity = 0,
        decimal RemainingQuantity = 0,
        string? BaseUnit = "قطعة",
        string? ParentUnit = "كرتونة",
        int ConversionFactor = 1);

    public sealed record PurchaseDetailResponse(
        Guid Id,
        string InvoiceNumber,
        string? InternalNumber,
        DateTime PurchaseDate,
        Guid SupplierId,
        string? SupplierName,
        decimal SubTotal,
        decimal DiscountAmount,
        decimal TaxAmount,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal RemainingAmount,
        string Status,
        string? Notes,
        IReadOnlyList<PurchaseDetailItemResponse> Items);
}
