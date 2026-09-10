using Dashboard.Application.Dashboard.Queries.GetDashboard;
using Dashboard.Application.Dashboard.Queries.GetDaySalesDetails;
using Dashboard.Application.Dashboard.Queries.GetMonthlySalesCalendar;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace POS.WebAPI.Controllers.Dashboard
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly IMediator _sender;

        public DashboardController(IMediator sender)
        {
            _sender = sender;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            CancellationToken ct = default)
        {
            var result = await _sender.Send(new GetDashboardQuery(fromDate, toDate), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }

        [HttpGet("monthly-calendar")]
        public async Task<IActionResult> GetMonthlyCalendar(
            [FromQuery] int year,
            [FromQuery] int month,
            [FromQuery] string? paymentMethod = null,
            [FromQuery] Guid? cashierId = null,
            CancellationToken ct = default)
        {
            var result = await _sender.Send(new GetMonthlySalesCalendarQuery(year, month, paymentMethod, cashierId), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }

        [HttpGet("day-sales-details")]
        public async Task<IActionResult> GetDaySalesDetails(
            [FromQuery] DateTime date,
            [FromQuery] string? paymentMethod = null,
            [FromQuery] Guid? cashierId = null,
            CancellationToken ct = default)
        {
            var result = await _sender.Send(new GetDaySalesDetailsQuery(date, paymentMethod, cashierId), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }
    }
}

