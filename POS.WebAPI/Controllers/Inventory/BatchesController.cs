using Inventory.Application.Batches.ProductBatches.Commands.ReplenishBatch;
using Inventory.Application.Batches.ProductBatches.Commands.ReplaceSupplierBatch;
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

        [HttpPost("{id:guid}/replenish")]
        public async Task<IActionResult> Replenish([FromRoute] Guid id, [FromBody] ReplenishBatchApiRequest req, CancellationToken ct)
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            Guid? userId = claim != null && Guid.TryParse(claim.Value, out var parsed) ? parsed : null;

            var command = new ReplenishBatchCommand(id, req.Quantity, req.Notes, userId);
            var result = await _sender.Send(command, ct);

            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok();
        }

        [HttpPost("{id:guid}/replace-supplier")]
        public async Task<IActionResult> ReplaceSupplier([FromRoute] Guid id, [FromBody] ReplaceSupplierBatchApiRequest req, CancellationToken ct)
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            Guid? userId = claim != null && Guid.TryParse(claim.Value, out var parsed) ? parsed : null;

            var command = new ReplaceSupplierBatchCommand(
                id,
                req.NewExpiryDate,
                req.NewBatchNumber,
                req.Notes,
                req.RelatedNotificationId,
                userId);

            var result = await _sender.Send(command, ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(new { success = true, message = "تم استبدال البضاعة من المورد وتحديث تاريخ الصلاحية بنجاح." });
        }
    }

    public sealed record ReplenishBatchApiRequest(decimal Quantity, string? Notes = null);
    public sealed record ReplaceSupplierBatchApiRequest(DateTime NewExpiryDate, string? NewBatchNumber = null, string? Notes = null, Guid? RelatedNotificationId = null);
}
