namespace Sales.Application.Sales.Commands.CreateSale
{
    public sealed record CreateSaleResult(
        Guid SaleId,
        IReadOnlyList<DepletedBatchDto> DepletedBatches
    );
}
