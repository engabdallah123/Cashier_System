using Inventory.Application.Batches.Queries.GetProductBatches;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace POS.WebAPI.Controllers.Inventory
{
    [ApiController]
    [Route("api/inventory/batches")]
    [Route("api/batches")]
    public class BatchesController : ControllerBase
    {
        private readonly ISender _sender;

        public BatchesController(ISender sender)
        {
            _sender = sender;
        }

        [HttpGet("product/{productId:guid}")]
        public async Task<IActionResult> GetProductBatches([FromRoute] Guid productId, CancellationToken ct)
        {
            var query = new GetProductBatchesQuery(productId);
            var result = await _sender.Send(query, ct);

            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }
    }
}
