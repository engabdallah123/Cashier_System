namespace Sales.Application.Sales.Queries
{
    public sealed record SaleItemResponse(
        Guid Id,
        Guid ProductId,
        string? ProductName,
        string? Barcode,
        decimal Quantity,
        decimal UnitPrice,
        decimal Discount,
        decimal Tax,
        decimal Total,
        string? UnitName = null,
        string? PriceType = null,
        string? PackagingInfo = null);

    public sealed class SaleResponse
    {
        public Guid Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public Guid CashierId { get; set; }
        public string? CashierName { get; set; }
        public Guid? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public Guid ShiftId { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal ChangeAmount { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public string Status { get; set; } = "Completed";
        public string? Notes { get; set; }
        public IReadOnlyList<SaleItemResponse> Items { get; set; } = Array.Empty<SaleItemResponse>();
    }

    public sealed record ReceiptResponse(
        string StoreName,
        string? Address,
        string? Phone,
        string InvoiceNumber,
        DateTime SaleDate,
        string CashierName,
        string? CustomerName,
        IReadOnlyList<SaleItemResponse> Items,
        decimal SubTotal,
        decimal DiscountAmount,
        decimal TaxAmount,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal ChangeAmount,
        string PaymentMethod,
        string Currency,
        string? InvoiceFooterMessage,
        string? LogoUrl = null,
        byte[]? LogoBytes = null,
        string? OrderType = null,
        int OrderNumber = 1);
}

