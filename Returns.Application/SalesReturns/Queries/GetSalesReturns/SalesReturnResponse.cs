namespace Returns.Application.SalesReturns.Queries.GetSalesReturns
{
    public sealed record SalesReturnResponse(
        Guid Id,
        string ReturnNumber,
        Guid OriginalSaleId,
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
        string Status);
}
