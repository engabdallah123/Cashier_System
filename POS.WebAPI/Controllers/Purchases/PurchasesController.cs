using MediatR;
using Microsoft.AspNetCore.Mvc;
using Purchases.Application.Purchases.Commands.CreatePurchase;
using Purchases.Application.Purchases.Commands.ReceivePurchase;
using Purchases.Application.Purchases.Queries.GetPurchases;
using Purchases.Application.Purchases.Commands.PayPurchaseInvoice;
using Purchases.Application.Purchases.Queries.GetPurchaseById;

using Purchases.Application.Purchases.Commands.DeletePurchase;
using Purchases.Application.Purchases.Commands.UpdatePurchase;

namespace POS.WebAPI.Controllers.Purchases
{
    [ApiController]
    [Route("api/[controller]")]
    public class PurchasesController : ControllerBase
    {
        private readonly IMediator _sender;

        public PurchasesController(IMediator sender)
        {
            _sender = sender;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePurchaseCommand command, CancellationToken ct)
        {
            var result = await _sender.Send(command, ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePurchaseCommand command, CancellationToken ct)
        {
            if (id != command.Id)
                return BadRequest(new { code = "Purchase.IdMismatch", message = "معرف الفاتورة غير متطابق." });

            var result = await _sender.Send(command, ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new DeletePurchaseCommand(id), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return NoContent();
        }

        [HttpPost("{id:guid}/receive")]
        public async Task<IActionResult> Receive(Guid id, [FromBody] Guid userId, CancellationToken ct)
        {
            var result = await _sender.Send(new ReceivePurchaseCommand(id, userId), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return NoContent();
        }

        [HttpPost("{id:guid}/pay")]
        public async Task<IActionResult> Pay(Guid id, [FromBody] decimal amount, CancellationToken ct)
        {
            var result = await _sender.Send(new PayPurchaseInvoiceCommand(id, amount), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return NoContent();
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] Guid? supplierId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        {
            var result = await _sender.Send(new GetPurchasesQuery(supplierId, page, pageSize), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }

        [HttpGet("expiring")]
        public async Task<IActionResult> GetExpiring([FromQuery] int? daysThreshold, CancellationToken ct)
        {
            var result = await _sender.Send(new global::Purchases.Application.Purchases.Queries.GetExpiringProducts.GetExpiringProductsQuery(daysThreshold), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new GetPurchaseByIdQuery(id), ct);
            if (result.IsFailure)
                return NotFound(result.Error);

            return Ok(result.Value);
        }
    }
}
