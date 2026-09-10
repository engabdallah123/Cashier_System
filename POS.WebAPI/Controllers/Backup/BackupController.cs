using Microsoft.AspNetCore.Mvc;
using POS.WebAPI.Services;
using System.Text.Json;

namespace POS.WebAPI.Controllers.Backup
{
    [ApiController]
    [Route("api/backup")]
    public class BackupController : ControllerBase
    {
        private readonly IBackupService _backupService;

        public BackupController(IBackupService backupService)
        {
            _backupService = backupService;
        }

        [HttpGet("export")]
        public async Task<IActionResult> Export(CancellationToken ct)
        {
            var (fileBytes, fileName) = await _backupService.ExportBackupAsync(ct);
            return File(fileBytes, "application/json", fileName);
        }

        [HttpPost("restore")]
        public async Task<IActionResult> Restore([FromBody] JsonElement rootElement, CancellationToken ct)
        {
            var (success, message, totalRows, rowsPerTable) = await _backupService.RestoreBackupAsync(rootElement, ct);

            if (!success)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = message
                });
            }

            return Ok(new
            {
                Success = true,
                Message = message,
                TotalRows = totalRows,
                RowsPerTable = rowsPerTable
            });
        }
    }
}
