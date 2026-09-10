using MediatR;
using Microsoft.AspNetCore.Mvc;
using Purchases.Application.Suppliers.Commands.CreateSupplier;
using Purchases.Application.Suppliers.Queries.GetSuppliers;

using Purchases.Application.Suppliers.Commands.UpdateSupplier;

namespace POS.WebAPI.Controllers.Purchases
{
    [ApiController]
    [Route("api/[controller]")]
    public class SuppliersController : ControllerBase
    {
        private readonly IMediator _sender;

        public SuppliersController(IMediator sender)
        {
            _sender = sender;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSupplierCommand command, CancellationToken ct)
        {
            var result = await _sender.Send(command, ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSupplierCommand command, CancellationToken ct)
        {
            if (id != command.Id)
                return BadRequest(new { code = "Supplier.IdMismatch", message = "معرف المورد غير متطابق." });

            var result = await _sender.Send(command, ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return NoContent();
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var result = await _sender.Send(new GetSuppliersQuery(), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }
    }
}
