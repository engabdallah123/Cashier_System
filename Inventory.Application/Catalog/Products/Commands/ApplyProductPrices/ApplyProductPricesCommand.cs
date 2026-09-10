using POS.Shared.Application.Messaging;

namespace Inventory.Application.Catalog.Products.Commands.ApplyProductPrices
{
    public sealed record ApplyProductPricesCommand(
        Guid ProductId,
        decimal CostPrice,
        decimal SellingPrice,
        decimal WholesalePrice) : ICommand;
}
