namespace Dashboard.Application.Dashboard.Queries.GetDaySalesDetails
{
    public sealed record DayInvoiceSummaryDto(
        Guid Id,
        string InvoiceNumber,
        DateTime SaleDate,
        Guid CashierId,
        string CashierName,
        Guid? CustomerId,
        string CustomerName,
        decimal SubTotal,
        decimal DiscountAmount,
        decimal TaxAmount,
        decimal TotalAmount,
        decimal PaidAmount,
        string PaymentMethod,
        int Status,
        int ItemCount,
        string? Notes);

    public sealed record DaySalesDetailsResponse(
        DateTime Date,
        string DayNameAr,
        decimal TotalSales,
        int TotalInvoices,
        decimal TotalReturns,
        decimal TotalExpenses,
        decimal TotalPurchases,
        IReadOnlyList<DayInvoiceSummaryDto> Invoices);
}
