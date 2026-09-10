namespace Returns.Application.SalesReturns.Queries
{
    public sealed record SalesReturnItemResponse(
        Guid Id,
        Guid ProductId,
        string? ProductName,
        string? Barcode,
        Guid OriginalSaleItemId,
        decimal Quantity,
        decimal UnitPrice,
        decimal Tax,
        decimal Total,
        string? Reason);

    public sealed record SalesReturnResponse(
        Guid Id,
        string ReturnNumber,
        Guid OriginalSaleId,
        string? OriginalInvoiceNumber,
        Guid CashierId,
        string? CashierName,
        Guid? CustomerId,
        string? CustomerName,
        Guid ShiftId,
        DateTime ReturnDate,
        decimal SubTotal,
        decimal TaxAmount,
        decimal TotalAmount,
        string RefundMethod,
        string? Reason,
        string? Notes,
        string Status,
        IReadOnlyList<SalesReturnItemResponse>? Items = null);
}
