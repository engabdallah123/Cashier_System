using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;

namespace POS.WebAPI.Services
{
    public class AutoBackupBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AutoBackupBackgroundService> _logger;
        private static readonly TimeSpan BackupInterval = TimeSpan.FromHours(12);

        public AutoBackupBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<AutoBackupBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AutoBackupBackgroundService is started. Automatic backups scheduled every 12 hours.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var backupFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "POS_Cashier_Backups");

                    DateTime? lastBackupUtc = null;

                    if (Directory.Exists(backupFolder))
                    {
                        var dir = new DirectoryInfo(backupFolder);
                        var latestFile = dir.GetFiles("POS_Backup_*.json")
                                            .OrderByDescending(f => f.LastWriteTimeUtc)
                                            .FirstOrDefault();
                        if (latestFile != null)
                        {
                            lastBackupUtc = latestFile.LastWriteTimeUtc;
                        }
                    }

                    TimeSpan delayUntilNextBackup;
                    if (lastBackupUtc.HasValue)
                    {
                        var elapsed = DateTime.UtcNow - lastBackupUtc.Value;
                        if (elapsed < BackupInterval && elapsed > TimeSpan.Zero)
                        {
                            delayUntilNextBackup = BackupInterval - elapsed;
                            _logger.LogInformation(
                                "Last backup was taken at {LastBackupUtc} UTC ({ElapsedHours:F1} hours ago). Next auto-backup scheduled in {RemainingHours:F1} hours.",
                                lastBackupUtc.Value,
                                elapsed.TotalHours,
                                delayUntilNextBackup.TotalHours);
                        }
                        else
                        {
                            // More than 12 hours elapsed since last backup, run in 30 seconds after server startup
                            delayUntilNextBackup = TimeSpan.FromSeconds(30);
                            _logger.LogInformation("Last backup was over 12 hours ago. Automatic backup will execute in 30 seconds.");
                        }
                    }
                    else
                    {
                        // No previous backup found, run in 30 seconds
                        delayUntilNextBackup = TimeSpan.FromSeconds(30);
                        _logger.LogInformation("No previous backups found. Initial automatic backup will execute in 30 seconds.");
                    }

                    await Task.Delay(delayUntilNextBackup, stoppingToken);

                    await PerformBackupAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred in automatic 12-hour backup loop.");
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task PerformBackupAsync(CancellationToken ct)
        {
            try
            {
                var backupFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "POS_Cashier_Backups");

                if (!Directory.Exists(backupFolder))
                {
                    Directory.CreateDirectory(backupFolder);
                }

                using var scope = _serviceProvider.CreateScope();
                var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();

                var (fileBytes, fileName) = await backupService.ExportBackupAsync(ct);
                var filePath = Path.Combine(backupFolder, fileName);

                await File.WriteAllBytesAsync(filePath, fileBytes, ct);

                _logger.LogInformation("Automatic 12-hour backup completed successfully: {FilePath} ({SizeKb:F1} KB)", filePath, fileBytes.Length / 1024.0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create automatic backup.");
            }
        }
    }
}
