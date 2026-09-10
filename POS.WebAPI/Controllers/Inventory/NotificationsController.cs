using Inventory.Application.Notifications.Commands.ResolveExpiryNotification;
using Inventory.Application.Notifications.Commands.SnoozeExpiryNotification;
using Inventory.Application.Notifications.Queries.GetExpiryNotifications;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace POS.WebAPI.Controllers.Inventory
{
    public sealed record SnoozeNotificationRequest(int Hours = 24);

    [ApiController]
    [Route("api/inventory/notifications")]
    [Route("api/notifications")]
    public class NotificationsController : ControllerBase
    {
        private readonly ISender _sender;

        public NotificationsController(ISender sender)
        {
            _sender = sender;
        }

        [HttpGet("expiry")]
        public async Task<IActionResult> GetExpiryNotifications([FromQuery] bool includeSnoozed = false, CancellationToken ct = default)
        {
            var query = new GetExpiryNotificationsQuery(includeSnoozed);
            var result = await _sender.Send(query, ct);

            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }

        [HttpPost("{id:guid}/resolve")]
        public async Task<IActionResult> Resolve([FromRoute] Guid id, CancellationToken ct)
        {
            var userId = Guid.Empty;
            if (User?.Identity?.IsAuthenticated == true)
            {
                var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (claim != null && Guid.TryParse(claim.Value, out var parsed))
                    userId = parsed;
            }

            var command = new ResolveExpiryNotificationCommand(id, userId == Guid.Empty ? null : userId);
            var result = await _sender.Send(command, ct);

            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(new { success = true, message = "تم حل ومراجعة التنبيه بنجاح [كله تمام]." });
        }

        [HttpPost("{id:guid}/snooze")]
        public async Task<IActionResult> Snooze([FromRoute] Guid id, [FromBody] SnoozeNotificationRequest? request, CancellationToken ct)
        {
            int hours = request?.Hours ?? 24;
            var command = new SnoozeExpiryNotificationCommand(id, hours);
            var result = await _sender.Send(command, ct);

            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(new { success = true, message = $"تم تأجيل الإشعار لمدة {hours} ساعة بنجاح [Skip]." });
        }
    }
}
