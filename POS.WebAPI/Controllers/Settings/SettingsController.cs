using MediatR;
using Microsoft.AspNetCore.Mvc;
using POS.Shared.Application.IService;
using Settings.Application.StoreSettings.Commands.UpdateSettings;
using Settings.Application.StoreSettings.Queries.GetSettings;

namespace POS.WebAPI.Controllers.Settings
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly IMediator _sender;
        private readonly IFileService _fileService;

        public SettingsController(IMediator sender, IFileService fileService)
        {
            _sender = sender;
            _fileService = fileService;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            var result = await _sender.Send(new GetSettingsQuery(), ct);
            if (result.IsFailure)
                return NotFound(result.Error);

            return Ok(result.Value);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateSettingsCommand command, CancellationToken ct)
        {
            var result = await _sender.Send(command, ct);
            if (result.IsFailure)
                return BadRequest(result.Error);

            return NoContent();
        }

        [HttpPost("logo")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadLogo(IFormFile file, CancellationToken ct)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { message = "الملف غير صالح أو فارغ." });

            var uploadResult = await _fileService.UploadFileAsync(file, "uploads/logos");
            if (uploadResult.IsFailure)
                return BadRequest(uploadResult.Error);

            return Ok(new { logoUrl = uploadResult.Value });
        }
    }
}
