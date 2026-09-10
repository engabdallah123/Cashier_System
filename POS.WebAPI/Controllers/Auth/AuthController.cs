using Identity.Application.Auth.Commands.Login;
using Identity.Application.Auth.Commands.RefreshToken;
using Identity.Application.Auth.Commands.SetupInitialAdmin;
using Identity.Application.Auth.Queries.GetInitialSetupStatus;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace POS.WebAPI.Controllers.Auth
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _sender;

        public AuthController(IMediator sender)
        {
            _sender = sender;
        }

        [HttpGet("initial-setup-required")]
        public async Task<IActionResult> CheckInitialSetupRequired(CancellationToken ct)
        {
            var result = await _sender.Send(new GetInitialSetupStatusQuery(), ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(new { setupRequired = result.Value });
        }

        [HttpPost("setup-admin")]
        public async Task<IActionResult> SetupInitialAdmin([FromBody] SetupInitialAdminCommand command, CancellationToken ct)
        {
            var result = await _sender.Send(command, ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct)
        {
            var result = await _sender.Send(command, ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }


        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenCommand command, CancellationToken ct)
        {
            var result = await _sender.Send(command, ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return Ok(result.Value);
        }
    }
}
