namespace POS.Licensing.Models;

public sealed record LicenseValidationResult(
    LicenseValidationStatus Status,
    string Message,
    LicensePayload? Payload = null,
    int? RemainingDays = null
)
{
    public bool IsValid => Status == LicenseValidationStatus.Valid;
}
