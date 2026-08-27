namespace POS.Licensing.Models;

public enum LicenseValidationStatus
{
    Valid = 0,
    Missing = 1,
    InvalidFormat = 2,
    InvalidSignature = 3,
    MachineMismatch = 4,
    Expired = 5,
    ProductMismatch = 6,
    VersionNotSupported = 7,
    InvalidIssuedAt = 8,
    UnsupportedLicenseType = 9,
    ClockRollbackDetected = 10,
    Corrupted = 11
}
