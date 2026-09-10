using Microsoft.JSInterop;
using POS.Desktop.Services.Api;

namespace POS.Desktop.Services.State
{
    public class StoreStateContainer
    {
        public string StoreName { get; private set; } = "نظام الكاشير";
        public StoreSettingDto? Settings { get; private set; }
        public bool IsLoaded { get; private set; }
        public bool AutoPrintInvoiceAfterSale { get; set; } = true;

        public event Action? OnStateChanged;

        public void SetSettings(StoreSettingDto settings)
        {
            Settings = settings;
            if (!string.IsNullOrWhiteSpace(settings.StoreName))
            {
                StoreName = settings.StoreName;
            }
            AutoPrintInvoiceAfterSale = settings.AutoPrintInvoice;
            IsLoaded = true;
            NotifyStateChanged();
        }

        public async Task LoadSettingsAsync(PosApiClient apiClient, IJSRuntime? js = null)
        {
            try
            {
                var settings = await apiClient.GetSettingsAsync();
                if (settings != null)
                {
                    SetSettings(settings);
                }
            }
            catch
            {
                // Fallback retains existing StoreName
            }
        }

        public async Task SetAutoPrintAsync(bool enabled, PosApiClient? apiClient = null, IJSRuntime? js = null)
        {
            AutoPrintInvoiceAfterSale = enabled;
            if (Settings != null)
            {
                Settings = Settings with { AutoPrintInvoice = enabled };
            }

            if (apiClient != null)
            {
                try
                {
                    var current = await apiClient.GetSettingsAsync();
                    if (current != null)
                    {
                        var req = new UpdateStoreSettingRequest(
                            StoreName: current.StoreName,
                            Address: current.Address,
                            Phone: current.Phone,
                            TaxRate: current.TaxRate,
                            IsTaxIncluded: current.IsTaxIncluded,
                            Currency: current.Currency,
                            InvoiceFooterMessage: current.InvoiceFooterMessage,
                            AllowNegativeStock: current.AllowNegativeStock,
                            AutoPrintInvoice: enabled,
                            LogoUrl: current.LogoUrl);

                        await apiClient.UpdateSettingsAsync(req);
                        SetSettings(current with { AutoPrintInvoice = enabled });
                    }
                }
                catch { }
            }

            if (js != null)
            {
                try
                {
                    await js.InvokeVoidAsync("setPosSetting", "pos_auto_print", enabled ? "true" : "false");
                }
                catch { }
            }

            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnStateChanged?.Invoke();
    }
}
