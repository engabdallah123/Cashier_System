using POS.Shared.Application.Messaging;

namespace Inventory.Application.Batches.Queries.GetProductBatches
{
    public sealed record GetProductBatchesQuery(Guid ProductId) : IQuery<IReadOnlyList<ProductBatchDto>>;
}
