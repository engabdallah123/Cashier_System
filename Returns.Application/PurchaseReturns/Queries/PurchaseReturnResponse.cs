namespace Returns.Application.PurchaseReturns.Queries
{
    public sealed record PurchaseReturnItemResponse(
        Guid Id,
        Guid ProductId,
        string? ProductName,
        string? Barcode,
        decimal Quantity,
        decimal UnitCost,
        decimal Tax,
        decimal Total);

    public sealed record PurchaseReturnResponse(
        Guid Id,
        string ReturnNumber,
        Guid OriginalPurchaseId,
        string? OriginalInvoiceNumber,
        Guid SupplierId,
        string? SupplierName,
        DateTime ReturnDate,
        decimal SubTotal,
        decimal TaxAmount,
        decimal TotalAmount,
        string? Reason,
        string? Notes,
        string Status,
        Guid CreatedByUserId,
        string? CreatedByUserName = null,
        IReadOnlyList<PurchaseReturnItemResponse>? Items = null);
}
