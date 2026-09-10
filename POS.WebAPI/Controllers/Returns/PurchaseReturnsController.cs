using MediatR;
using Microsoft.AspNetCore.Mvc;
using Returns.Application.PurchaseReturns.Commands.CreatePurchaseReturn;
using Returns.Application.PurchaseReturns.Commands.DeletePurchaseReturn;
using Returns.Application.PurchaseReturns.Commands.UpdatePurchaseReturn;
using Returns.Application.PurchaseReturns.Queries.GetPurchaseReturnById;
using Returns.Application.PurchaseReturns.Queries.GetPurchaseReturns;

namespace POS.WebAPI.Controllers.Returns
{
    [ApiController]
    [Route("api/returns/purchases")]
    public class PurchaseReturnsController : ControllerBase
    {
        private readonly IMediator _sender;

        public PurchaseReturnsController(IMediator sender)
        {
            _sender = sender;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePurchaseReturnCommand command, CancellationToken ct)
        {
            var result = await _sender.Send(command, ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] Guid? supplierId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken ct = default)
        {
            var result = await _sender.Send(new GetPurchaseReturnsQuery(supplierId, page, pageSize), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new GetPurchaseReturnByIdQuery(id), ct);
            if (result.IsFailure)
                return NotFound(result.Error);

            return Ok(result.Value);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePurchaseReturnCommand command, CancellationToken ct)
        {
            if (id != command.Id)
                return BadRequest(new { code = "PurchaseReturn.IdMismatch", message = "معرف المرتجع غير متطابق." });

            var result = await _sender.Send(command, ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? userId = null, CancellationToken ct = default)
        {
            var result = await _sender.Send(new DeletePurchaseReturnCommand(id, userId ?? Guid.Empty), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return NoContent();
        }
    }
}
