namespace POS.Licensing.Models;

public sealed record SignedLicenseFile(
    LicensePayload Payload,
    string Signature
);
