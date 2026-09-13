namespace Sales.Application.Sales.Commands.CreateSale
{
    public sealed record DepletedBatchDto(
        Guid BatchId,
        Guid ProductId,
        string ProductName,
        string BatchNumber,
        decimal OriginalQuantity,
        string OriginalUnit,
        DateTime PurchaseDate,
        DateTime? ExpiryDate,
        Guid? PurchaseInvoiceId
    );
}
