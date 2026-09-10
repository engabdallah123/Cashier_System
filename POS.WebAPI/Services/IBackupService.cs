using System.Text.Json;

namespace POS.WebAPI.Services
{
    public interface IBackupService
    {
        Task<(byte[] FileBytes, string FileName)> ExportBackupAsync(CancellationToken ct = default);
        Task<(bool Success, string Message, int TotalRows, Dictionary<string, int> RowsPerTable)> RestoreBackupAsync(JsonElement rootElement, CancellationToken ct = default);
    }
}
