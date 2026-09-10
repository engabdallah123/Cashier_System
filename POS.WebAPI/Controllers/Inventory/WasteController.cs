using Inventory.Application.Stock.Waste.Commands.RecordWaste;
using Inventory.Application.Stock.Waste.Queries.GetWasteReport;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace POS.WebAPI.Controllers.Inventory
{
    public sealed record RecordWasteRequest(
        Guid ProductId,
        Guid InventoryBatchId,
        decimal Quantity,
        string Unit,
        string Reason,
        string? Notes = null,
        string Source = "Manual",
        Guid? RelatedNotificationId = null);

    [ApiController]
    [Route("api/inventory/waste")]
    [Route("api/waste")]
    public class WasteController : ControllerBase
    {
        private readonly ISender _sender;

        public WasteController(ISender sender)
        {
            _sender = sender;
        }

        [HttpPost]
        public async Task<IActionResult> RecordWaste([FromBody] RecordWasteRequest request, CancellationToken ct)
        {
            // استخراج معرف المستخدم الحالي إن وجد أو توليد معرف نظام
            var userId = Guid.Empty;
            if (User?.Identity?.IsAuthenticated == true)
            {
                var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (claim != null && Guid.TryParse(claim.Value, out var parsed))
                    userId = parsed;
            }

            var command = new RecordWasteCommand(
                request.ProductId,
                request.InventoryBatchId,
                request.Quantity,
                request.Unit,
                request.Reason,
                userId,
                request.Notes,
                request.Source,
                request.RelatedNotificationId);

            var result = await _sender.Send(command, ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(new { id = result.Value, message = "تم تسجيل الهالك وخصم المخزون بنجاح." });
        }

        [HttpGet("report")]
        public async Task<IActionResult> GetReport(
            [FromQuery] Guid? productId,
            [FromQuery] string? reason,
            [FromQuery] string? source,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            CancellationToken ct)
        {
            var query = new GetWasteReportQuery(productId, reason, source, fromDate, toDate);
            var result = await _sender.Send(query, ct);

            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }
    }
}
