namespace Settings.Application.StoreSettings.Queries
{
    public sealed record StoreSettingResponse(
        Guid Id,
        string StoreName,
        string? Address,
        string? Phone,
        decimal TaxRate,
        bool IsTaxIncluded,
        string Currency,
        string? InvoiceFooterMessage,
        bool AllowNegativeStock,
        bool AutoPrintInvoice,
        bool EnableDepletedBatchAlert,
        string? LogoUrl,
        DateTime UpdatedAt);
}
