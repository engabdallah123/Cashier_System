namespace POS.Licensing.Models;

public sealed record LicensePayload(
    string LicenseId,
    string Product,
    string CustomerName,
    string MachineId,
    LicenseType LicenseType,
    DateTime IssuedAtUtc,
    DateTime? ExpiresAtUtc,
    string Version,
    string? MinSupportedVersion = null,
    string? MaxSupportedVersion = null,
    Dictionary<string, string>? Features = null,
    string? Notes = null
);
