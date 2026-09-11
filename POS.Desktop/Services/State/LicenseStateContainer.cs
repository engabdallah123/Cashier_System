using POS.Licensing.Cryptography;
using POS.Licensing.Interfaces;
using POS.Licensing.Models;

namespace POS.Desktop.Services.State;

public class LicenseStateContainer
{
    private readonly ILicenseValidator _licenseValidator;
    private readonly ILicenseStorage _licenseStorage;
    private readonly IMachineIdProvider _machineIdProvider;

    public LicenseValidationResult CurrentResult { get; private set; }
    public string MachineId => _machineIdProvider.GetMachineId();
    public bool IsLicensed => CurrentResult.IsValid;

    public event Action? OnLicenseStateChanged;

    public LicenseStateContainer(
        ILicenseValidator licenseValidator,
        ILicenseStorage licenseStorage,
        IMachineIdProvider machineIdProvider)
    {
        _licenseValidator = licenseValidator;
        _licenseStorage = licenseStorage;
        _machineIdProvider = machineIdProvider;

        // Perform initial validation
        CurrentResult = _licenseValidator.Validate();
    }

    public LicenseValidationResult Refresh()
    {
        CurrentResult = _licenseValidator.Validate();
        NotifyStateChanged();
        return CurrentResult;
    }

    public LicenseValidationResult Activate(SignedLicenseFile signedLicense)
    {
        var validation = _licenseValidator.Validate(signedLicense);
        if (validation.IsValid)
        {
            _licenseStorage.SaveLicense(signedLicense);
            CurrentResult = validation;
            NotifyStateChanged();
        }
        return validation;
    }

    public LicenseValidationResult ActivateFromJson(string jsonContent)
    {
        var signed = CanonicalLicenseSerializer.DeserializeLicense(jsonContent);
        if (signed is null)
        {
            return new LicenseValidationResult(
                LicenseValidationStatus.InvalidFormat,
                "صيغة ملف الترخيص غير صحيحة.");
        }

        return Activate(signed);
    }

    public void RemoveLicense()
    {
        _licenseStorage.DeleteLicense();
        CurrentResult = _licenseValidator.Validate();
        NotifyStateChanged();
    }

    public LicenseValidationResult ResetClockAndRevalidate()
    {
        _licenseStorage.ResetWatermark();
        CurrentResult = _licenseValidator.Validate();
        NotifyStateChanged();
        return CurrentResult;
    }

    private void NotifyStateChanged() => OnLicenseStateChanged?.Invoke();
}
