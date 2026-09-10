using MediatR;
using Microsoft.AspNetCore.Mvc;
using Returns.Application.SalesReturns.Commands.CreateSalesReturn;
using Returns.Application.SalesReturns.Commands.DeleteSalesReturn;
using Returns.Application.SalesReturns.Commands.UpdateSalesReturn;
using Returns.Application.SalesReturns.Queries.GetSalesReturnById;
using Returns.Application.SalesReturns.Queries.GetSalesReturns;

namespace POS.WebAPI.Controllers.Returns
{
    [ApiController]
    [Route("api/returns/sales")]
    public class SalesReturnsController : ControllerBase
    {
        private readonly IMediator _sender;

        public SalesReturnsController(IMediator sender)
        {
            _sender = sender;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSalesReturnCommand command, CancellationToken ct)
        {
            var result = await _sender.Send(command, ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] Guid? cashierId = null,
            [FromQuery] Guid? shiftId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken ct = default)
        {
            var result = await _sender.Send(new GetSalesReturnsQuery(cashierId, shiftId, page, pageSize), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new GetSalesReturnByIdQuery(id), ct);
            if (result.IsFailure)
                return NotFound(result.Error);

            return Ok(result.Value);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSalesReturnCommand command, CancellationToken ct)
        {
            if (id != command.Id)
                return BadRequest(new { code = "SalesReturn.IdMismatch", message = "معرف المرتجع غير متطابق." });

            var result = await _sender.Send(command, ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? userId = null, CancellationToken ct = default)
        {
            var result = await _sender.Send(new DeleteSalesReturnCommand(id, userId ?? Guid.Empty), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return NoContent();
        }
    }
}
