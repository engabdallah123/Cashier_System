using POS.Shared.Application.Messaging;

namespace Settings.Application.StoreSettings.Commands.UpdateSettings
{
    public sealed record UpdateSettingsCommand(
        string StoreName,
        string? Address,
        string? Phone,
        decimal TaxRate,
        bool IsTaxIncluded,
        string Currency,
        string? InvoiceFooterMessage,
        bool AllowNegativeStock,
        bool AutoPrintInvoice = true,
        string? LogoUrl = null) : ICommand;
}
