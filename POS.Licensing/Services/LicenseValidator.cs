using POS.Licensing.Cryptography;
using POS.Licensing.Interfaces;
using POS.Licensing.Models;

namespace POS.Licensing.Services;

/// <summary>
/// Validates license files against cryptographic signatures, machine bindings, dates, and versions.
/// </summary>
public sealed class LicenseValidator : ILicenseValidator
{
    public const string DefaultProduct = "POS Supermarket Cashier System";
    public const string DefaultCurrentVersion = "1.0.0";

    private readonly IMachineIdProvider _machineIdProvider;
    private readonly ILicenseStorage _licenseStorage;
    private readonly string _publicKeyPem;
    private readonly string _expectedProduct;
    private readonly string _currentVersion;

    public LicenseValidator(
        IMachineIdProvider machineIdProvider,
        ILicenseStorage licenseStorage,
        string? publicKeyPem = null,
        string? expectedProduct = null,
        string? currentVersion = null)
    {
        _machineIdProvider = machineIdProvider ?? throw new ArgumentNullException(nameof(machineIdProvider));
        _licenseStorage = licenseStorage ?? throw new ArgumentNullException(nameof(licenseStorage));
        _publicKeyPem = string.IsNullOrWhiteSpace(publicKeyPem) ? EmbeddedKeys.PublicKeyPem : publicKeyPem;
        _expectedProduct = string.IsNullOrWhiteSpace(expectedProduct) ? DefaultProduct : expectedProduct;
        _currentVersion = string.IsNullOrWhiteSpace(currentVersion) ? DefaultCurrentVersion : currentVersion;
    }

    public LicenseValidationResult Validate(SignedLicenseFile? license = null)
    {
        var targetLicense = license ?? _licenseStorage.LoadLicense();

        // 1. Check if license file exists
        if (targetLicense is null)
        {
            return new LicenseValidationResult(
                LicenseValidationStatus.Missing,
                "ملف الترخيص غير موجود. الرجاء تفعيل البرنامج عبر استيراد ملف ترخيص صالح.");
        }

        // 2. Validate payload presence and basic structure
        if (targetLicense.Payload is null || string.IsNullOrWhiteSpace(targetLicense.Signature))
        {
            return new LicenseValidationResult(
                LicenseValidationStatus.InvalidFormat,
                "صيغة ملف الترخيص غير صالحة أو تالفة.");
        }

        var payload = targetLicense.Payload;

        // 3. Cryptographic Signature Verification
        if (!LicenseSignatureVerifier.Verify(targetLicense, _publicKeyPem))
        {
            return new LicenseValidationResult(
                LicenseValidationStatus.InvalidSignature,
                "التوقيع الرقمي للترخيص غير صالح أو تم التلاعب بمحتوى الملف.",
                payload);
        }

        // 4. Product Verification
        if (!string.Equals(payload.Product?.Trim(), _expectedProduct.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return new LicenseValidationResult(
                LicenseValidationStatus.ProductMismatch,
                $"هذا الترخيص مخصص لمنتج '{payload.Product}' وليس متوافقاً مع '{_expectedProduct}'.",
                payload);
        }

        // 5. Machine Binding Verification
        if (!_machineIdProvider.ValidateMachineId(payload.MachineId))
        {
            return new LicenseValidationResult(
                LicenseValidationStatus.MachineMismatch,
                $"الترخيص غير مسجل على هذا الجهاز (معرف الجهاز: {_machineIdProvider.GetMachineId()}).",
                payload);
        }

        var nowUtc = DateTime.UtcNow;

        // 6. Clock Rollback / Tamper Detection
        if (_licenseStorage.CheckClockRollback(nowUtc, TimeSpan.FromMinutes(10)))
        {
            return new LicenseValidationResult(
                LicenseValidationStatus.ClockRollbackDetected,
                "تم اكتشاف تراجع في ساعة النظام (Clock Rollback). يرجى ضبط تاريخ ووقت الويندوز بدقة.",
                payload);
        }

        // 7. Check Issued Date (not in future beyond 10 min tolerance)
        if (payload.IssuedAtUtc > nowUtc.AddMinutes(10))
        {
            return new LicenseValidationResult(
                LicenseValidationStatus.InvalidIssuedAt,
                "تاريخ إصدار الترخيص غير صالح (تاريخ مستقبلي).",
                payload);
        }

        // 8. Expiration Verification
        int? remainingDays = null;
        if (payload.LicenseType != LicenseType.Lifetime)
        {
            if (!payload.ExpiresAtUtc.HasValue)
            {
                return new LicenseValidationResult(
                    LicenseValidationStatus.InvalidFormat,
                    "تاريخ انتهاء الترخيص غير محدد.",
                    payload);
            }

            if (nowUtc > payload.ExpiresAtUtc.Value)
            {
                return new LicenseValidationResult(
                    LicenseValidationStatus.Expired,
                    $"انتهت صلاحية هذا الترخيص بتاريخ {payload.ExpiresAtUtc.Value:yyyy-MM-dd}. الرجاء تجديد الترخيص.",
                    payload,
                    0);
            }

            remainingDays = Math.Max(0, (int)(payload.ExpiresAtUtc.Value - nowUtc).TotalDays);
        }

        // 9. Version Compatibility Check
        if (!string.IsNullOrWhiteSpace(payload.MinSupportedVersion) &&
            Version.TryParse(payload.MinSupportedVersion, out var minVer) &&
            Version.TryParse(_currentVersion, out var curVer1) &&
            curVer1 < minVer)
        {
            return new LicenseValidationResult(
                LicenseValidationStatus.VersionNotSupported,
                $"إصدار البرنامج الحالي ({_currentVersion}) أقل من الحد الأدنى للترخيص ({payload.MinSupportedVersion}).",
                payload);
        }

        if (!string.IsNullOrWhiteSpace(payload.MaxSupportedVersion) &&
            Version.TryParse(payload.MaxSupportedVersion, out var maxVer) &&
            Version.TryParse(_currentVersion, out var curVer2) &&
            curVer2 > maxVer)
        {
            return new LicenseValidationResult(
                LicenseValidationStatus.VersionNotSupported,
                $"إصدار البرنامج الحالي ({_currentVersion}) يتجاوز الحد الأقصى المدعوم بالترخيص ({payload.MaxSupportedVersion}).",
                payload);
        }

        // All checks passed!
        return new LicenseValidationResult(
            LicenseValidationStatus.Valid,
            "الترخيص صالح ومفعل بنجاح.",
            payload,
            remainingDays);
    }
}
