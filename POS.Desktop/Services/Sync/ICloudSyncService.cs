namespace POS.Desktop.Services.Sync
{
    public interface ICloudSyncService : IDisposable
    {
        event Action? OnSyncStateChanged;

        bool IsSyncing { get; }
        bool IsOffline { get; }
        DateTime? LastSyncTime { get; }
        string? LastSyncStatus { get; }
        bool IsCloudReachable { get; }
        bool? LastSyncSucceeded { get; }

        Task<bool> CheckCloudOnlineAsync();
        Task<SyncStatusResult> SyncNowAsync(bool forceCatalogPush = false);
        Task<bool> PushClosedShiftToCloudAsync(PushClosedShiftRequest shiftReq);
        void RecordLocalChange();
        void NotifyLocalChange() => RecordLocalChange();
        void StartPeriodicSync(TimeSpan? interval = null);
        void StopPeriodicSync();
    }
}
