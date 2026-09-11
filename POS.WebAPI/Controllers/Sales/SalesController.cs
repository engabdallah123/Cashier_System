using MediatR;
using Microsoft.AspNetCore.Mvc;
using Sales.Application.Sales.Commands.CreateSale;
using Sales.Application.Sales.Queries.GetSaleById;
using Sales.Application.Sales.Queries.GetSalePdf;
using Sales.Application.Sales.Queries.GetSaleReceipt;
using Sales.Application.Sales.Queries.GetSales;

namespace POS.WebAPI.Controllers.Sales
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalesController : ControllerBase
    {
        private readonly IMediator _sender;

        public SalesController(IMediator sender)
        {
            _sender = sender;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSaleCommand command, CancellationToken ct)
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
            [FromQuery] Guid? customerId = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken ct = default)
        {
            var result = await _sender.Send(new GetSalesQuery(cashierId, shiftId, customerId, fromDate, toDate, page, pageSize), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new GetSaleByIdQuery(id), ct);
            if (result.IsFailure)
                return NotFound(result.Error);

            return Ok(result.Value);
        }

        [HttpGet("by-invoice/{invoiceNumber}")]
        public async Task<IActionResult> GetByInvoiceNumber(string invoiceNumber, CancellationToken ct)
        {
            var result = await _sender.Send(new global::Sales.Application.Sales.Queries.GetSaleByInvoiceNumber.GetSaleByInvoiceNumberQuery(invoiceNumber), ct);
            if (result.IsFailure)
                return NotFound(result.Error);

            return Ok(result.Value);
        }

        [HttpPost("{id:guid}/pay")]
        public async Task<IActionResult> Pay(Guid id, [FromBody] decimal amount, CancellationToken ct)
        {
            var result = await _sender.Send(new global::Sales.Application.Sales.Commands.PaySaleInvoice.PaySaleInvoiceCommand(id, amount), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return NoContent();
        }

        [HttpGet("{id:guid}/receipt")]
        public async Task<IActionResult> GetReceipt(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new GetSaleReceiptQuery(id), ct);
            if (result.IsFailure)
                return NotFound(result.Error);

            return Ok(result.Value);
        }

        [HttpGet("{id:guid}/pdf")]
        public async Task<IActionResult> GetPdf(Guid id, [FromQuery] bool isThermal = false, CancellationToken ct = default)
        {
            var result = await _sender.Send(new GetSalePdfQuery(id, isThermal), ct);
            if (result.IsFailure)
                return NotFound(result.Error);

            return File(result.Value!, "application/pdf", $"Invoice_{id}.pdf");
        }
    }
}
