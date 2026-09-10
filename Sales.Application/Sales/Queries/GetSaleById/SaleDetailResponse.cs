namespace Sales.Application.Sales.Queries.GetSaleById
{
    public sealed record SaleDetailItemResponse(
        Guid Id,
        Guid ProductId,
        string? ProductName,
        string? Barcode,
        decimal Quantity,
        decimal UnitPrice,
        decimal Discount,
        decimal Tax,
        decimal Total,
        decimal ReturnedQuantity = 0,
        decimal RemainingQuantity = 0,
        string? BaseUnit = "قطعة",
        string? ParentUnit = "كرتونة",
        int ConversionFactor = 1);

    public sealed record SaleDetailResponse(
        Guid Id,
        string InvoiceNumber,
        DateTime SaleDate,
        Guid CashierId,
        string? CashierName,
        Guid? CustomerId,
        string? CustomerName,
        Guid ShiftId,
        decimal SubTotal,
        decimal DiscountAmount,
        decimal TaxAmount,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal ChangeAmount,
        string PaymentMethod,
        string Status,
        string? Notes,
        IReadOnlyList<SaleDetailItemResponse> Items);
}
