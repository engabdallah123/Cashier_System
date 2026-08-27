using POS.Licensing.Models;

namespace POS.Licensing.Interfaces;

public interface ILicenseValidator
{
    LicenseValidationResult Validate(SignedLicenseFile? license = null);
}
