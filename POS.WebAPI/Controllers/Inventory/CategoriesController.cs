using Inventory.Application.Catalog.Categories.Commands.ActivateCategory;
using Inventory.Application.Catalog.Categories.Commands.CreateCategory;
using Inventory.Application.Catalog.Categories.Commands.DeactivateCategory;
using Inventory.Application.Catalog.Categories.Commands.DeleteCategory;
using Inventory.Application.Catalog.Categories.Commands.UpdateCategory;
using Inventory.Application.Catalog.Categories.Queries.GetCategories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace POS.WebAPI.Controllers.Inventory
{
    [ApiController]
    [Route("api/inventory/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly IMediator _sender;

        public CategoriesController(IMediator sender)
        {
            _sender = sender;
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager,Cashier,Administrator")]
        public async Task<IActionResult> Create([FromBody] CreateCategoryCommand command, CancellationToken ct)
        {
            var result = await _sender.Send(command, ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin,Manager,Cashier,Administrator")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryCommand command, CancellationToken ct)
        {
            if (id != command.Id)
                return BadRequest("ID in URL does not match request body.");

            var result = await _sender.Send(command, ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return NoContent();
        }

        [HttpPut("{id:guid}/activate")]
        [Authorize(Roles = "Admin,Manager,Cashier,Administrator")]
        public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new ActivateCategoryCommand(id), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return NoContent();
        }

        [HttpPut("{id:guid}/deactivate")]
        [Authorize(Roles = "Admin,Manager,Cashier,Administrator")]
        public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new DeactivateCategoryCommand(id), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin,Manager,Cashier,Administrator")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var result = await _sender.Send(new DeleteCategoryCommand(id), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return NoContent();
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool? onlyActive, CancellationToken ct)
        {
            var result = await _sender.Send(new GetCategoriesQuery(onlyActive), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }
    }
}
