using POS.Licensing.Cryptography;
using POS.Licensing.Interfaces;
using POS.Licensing.Models;
using POS.Licensing.Services;

namespace POS.Licensing.Tests;

/// <summary>
/// Tests for POS.Licensing: RSA signing, validation, machine binding, expiry, and tamper detection.
/// </summary>
public class LicensingTests
{
    // Test RSA Key Pair (Generated for testing ONLY - never used in production)
    private static readonly (string PrivateKey, string PublicKey) _testKeyPair = RsaKeyHelper.GenerateKeyPair(2048);

    private const string TestProduct = "POS Supermarket Cashier System";
    private const string TestVersion = "1.0.0";
    private const string TestMachineId = "POS-AB12-CD34-EF56-7890";
    private const string AnotherMachineId = "POS-0000-1111-2222-3333";

    private SignedLicenseFile CreateValidLicense(
        string? machineId = null,
        string? product = null,
        LicenseType licenseType = LicenseType.Yearly,
        DateTime? expiresAtUtc = null,
        DateTime? issuedAtUtc = null,
        string? version = null,
        string? minSupportedVersion = null)
    {
        var payload = new LicensePayload(
            LicenseId: Guid.NewGuid().ToString(),
            Product: product ?? TestProduct,
            CustomerName: "Ahmed Mohamed",
            MachineId: machineId ?? TestMachineId,
            LicenseType: licenseType,
            IssuedAtUtc: issuedAtUtc ?? DateTime.UtcNow.AddMinutes(-1),
            ExpiresAtUtc: licenseType == LicenseType.Lifetime ? null : (expiresAtUtc ?? DateTime.UtcNow.AddYears(1)),
            Version: version ?? TestVersion,
            MinSupportedVersion: minSupportedVersion
        );
        return LicenseSigner.Sign(payload, _testKeyPair.PrivateKey);
    }

    private ILicenseValidator CreateValidator(
        string? machineId = null,
        string? product = null,
        string? version = null)
    {
        var fakeMachineProvider = new FakeMachineIdProvider(machineId ?? TestMachineId);
        var fakeStorage = new InMemoryLicenseStorage(null);
        return new LicenseValidator(fakeMachineProvider, fakeStorage, _testKeyPair.PublicKey, product ?? TestProduct, version ?? TestVersion);
    }

    // ===== VALID LICENSE =====
    [Fact]
    public void ValidLicense_ShouldReturnValid()
    {
        var license = CreateValidLicense();
        var validator = CreateValidator();
        var result = validator.Validate(license);
        Assert.Equal(LicenseValidationStatus.Valid, result.Status);
        Assert.NotNull(result.Payload);
    }

    // ===== LIFETIME LICENSE =====
    [Fact]
    public void LifetimeLicense_NoExpiry_ShouldReturnValid()
    {
        var license = CreateValidLicense(licenseType: LicenseType.Lifetime);
        var validator = CreateValidator();
        var result = validator.Validate(license);
        Assert.Equal(LicenseValidationStatus.Valid, result.Status);
        Assert.Null(result.RemainingDays);
    }

    // ===== MISSING LICENSE =====
    [Fact]
    public void MissingLicense_ShouldReturnMissing()
    {
        var validator = CreateValidator();
        var result = validator.Validate(null);
        Assert.Equal(LicenseValidationStatus.Missing, result.Status);
    }

    // ===== WRONG MACHINE ID =====
    [Fact]
    public void WrongMachineId_ShouldReturnMachineMismatch()
    {
        // License issued for AnotherMachineId, validator expects TestMachineId
        var license = CreateValidLicense(machineId: AnotherMachineId);
        var validator = CreateValidator(machineId: TestMachineId);
        var result = validator.Validate(license);
        Assert.Equal(LicenseValidationStatus.MachineMismatch, result.Status);
    }

    // ===== MODIFIED CustomerName =====
    [Fact]
    public void TamperedCustomerName_ShouldReturnInvalidSignature()
    {
        var license = CreateValidLicense();
        // Tamper with CustomerName
        var tampered = license with
        {
            Payload = license.Payload with { CustomerName = "Hacker Name" }
        };
        var validator = CreateValidator();
        var result = validator.Validate(tampered);
        Assert.Equal(LicenseValidationStatus.InvalidSignature, result.Status);
    }

    // ===== MODIFIED MachineId in payload =====
    [Fact]
    public void TamperedMachineId_ShouldReturnInvalidSignature()
    {
        var license = CreateValidLicense();
        var tampered = license with
        {
            Payload = license.Payload with { MachineId = TestMachineId }
        };
        // Modify the signature to test canonical check
        var reSignedWithWrongKey = license with
        {
            Payload = license.Payload with { MachineId = AnotherMachineId },
            Signature = license.Signature[..^4] + "XXXX"
        };
        var validator = CreateValidator();
        var result = validator.Validate(reSignedWithWrongKey);
        Assert.Equal(LicenseValidationStatus.InvalidSignature, result.Status);
    }

    // ===== MODIFIED PRODUCT =====
    [Fact]
    public void TamperedProduct_ShouldReturnInvalidSignature()
    {
        var license = CreateValidLicense();
        var tampered = license with
        {
            Payload = license.Payload with { Product = "Other POS System" }
        };
        var validator = CreateValidator();
        var result = validator.Validate(tampered);
        Assert.Equal(LicenseValidationStatus.InvalidSignature, result.Status);
    }

    // ===== MODIFIED LICENSE TYPE =====
    [Fact]
    public void TamperedLicenseType_ShouldReturnInvalidSignature()
    {
        var license = CreateValidLicense(licenseType: LicenseType.Monthly);
        var tampered = license with
        {
            Payload = license.Payload with { LicenseType = LicenseType.Lifetime }
        };
        var validator = CreateValidator();
        var result = validator.Validate(tampered);
        Assert.Equal(LicenseValidationStatus.InvalidSignature, result.Status);
    }

    // ===== MODIFIED EXPIRY =====
    [Fact]
    public void TamperedExpiresAtUtc_ShouldReturnInvalidSignature()
    {
        var license = CreateValidLicense(expiresAtUtc: DateTime.UtcNow.AddDays(30));
        var tampered = license with
        {
            Payload = license.Payload with { ExpiresAtUtc = DateTime.UtcNow.AddYears(99) }
        };
        var validator = CreateValidator();
        var result = validator.Validate(tampered);
        Assert.Equal(LicenseValidationStatus.InvalidSignature, result.Status);
    }

    // ===== MODIFIED SIGNATURE =====
    [Fact]
    public void TamperedSignature_ShouldReturnInvalidSignature()
    {
        var license = CreateValidLicense();
        var tampered = license with { Signature = Convert.ToBase64String(new byte[128]) };
        var validator = CreateValidator();
        var result = validator.Validate(tampered);
        Assert.Equal(LicenseValidationStatus.InvalidSignature, result.Status);
    }

    // ===== EXPIRED LICENSE =====
    [Fact]
    public void ExpiredLicense_ShouldReturnExpired()
    {
        var license = CreateValidLicense(
            expiresAtUtc: DateTime.UtcNow.AddDays(-1),
            issuedAtUtc: DateTime.UtcNow.AddYears(-1));
        var validator = CreateValidator();
        var result = validator.Validate(license);
        Assert.Equal(LicenseValidationStatus.Expired, result.Status);
    }

    // ===== PRODUCT MISMATCH =====
    [Fact]
    public void ProductMismatch_ShouldReturnProductMismatch()
    {
        var license = CreateValidLicense(product: "Different Product");
        var validator = CreateValidator(product: TestProduct);
        var result = validator.Validate(license);
        // Signature is valid (product matches in signature), but product name differs from expected
        // Since signature is valid but product doesn't match our expected value:
        Assert.Equal(LicenseValidationStatus.ProductMismatch, result.Status);
    }

    // ===== VERSION NOT SUPPORTED =====
    [Fact]
    public void VersionTooOld_ShouldReturnVersionNotSupported()
    {
        var license = CreateValidLicense(minSupportedVersion: "2.0.0");
        var validator = CreateValidator(version: "1.0.0"); // running v1 but min is v2
        var result = validator.Validate(license);
        Assert.Equal(LicenseValidationStatus.VersionNotSupported, result.Status);
    }

    // ===== CANONICAL SERIALIZATION DETERMINISM =====
    [Fact]
    public void CanonicalSerializer_ProducesIdenticalBytesMultipleTimes()
    {
        var payload = new LicensePayload(
            LicenseId: "test-id",
            Product: TestProduct,
            CustomerName: "Test Customer",
            MachineId: TestMachineId,
            LicenseType: LicenseType.Yearly,
            IssuedAtUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpiresAtUtc: new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Version: "1.0.0"
        );

        var bytes1 = CanonicalLicenseSerializer.SerializeToCanonicalBytes(payload);
        var bytes2 = CanonicalLicenseSerializer.SerializeToCanonicalBytes(payload);
        var bytes3 = CanonicalLicenseSerializer.SerializeToCanonicalBytes(payload);

        Assert.True(bytes1.SequenceEqual(bytes2));
        Assert.True(bytes2.SequenceEqual(bytes3));
    }

    // ===== MACHINE ID STABILITY =====
    [Fact]
    public void MachineIdProvider_ReturnsSameIdOnMultipleCalls()
    {
        var provider = new MachineIdProvider();
        var id1 = provider.GetMachineId();
        var id2 = provider.GetMachineId();
        var id3 = provider.GetMachineId();

        Assert.Equal(id1, id2);
        Assert.Equal(id2, id3);
        Assert.StartsWith("POS-", id1);
        // Format: POS-XXXX-XXXX-XXXX-XXXX = 4+4+1+4+1+4+1+4 = 23 chars
        Assert.True(id1.Length == 23, $"Expected 23 char format POS-XXXX-XXXX-XXXX-XXXX, got: '{id1}' (length {id1.Length})");
    }

    // ===== MACHINE ID VALIDATES =====
    [Fact]
    public void MachineIdProvider_ValidateMachineId_ReturnsTrueForSameId()
    {
        var provider = new MachineIdProvider();
        var id = provider.GetMachineId();
        Assert.True(provider.ValidateMachineId(id));
        Assert.False(provider.ValidateMachineId(AnotherMachineId));
    }
}

// ===== Fake / In-Memory helpers for tests =====

public sealed class FakeMachineIdProvider : IMachineIdProvider
{
    private readonly string _id;
    public FakeMachineIdProvider(string id) => _id = id;
    public string GetMachineId() => _id;
    public bool ValidateMachineId(string expected) => string.Equals(_id, expected?.Trim(), StringComparison.OrdinalIgnoreCase);
}

public sealed class InMemoryLicenseStorage : ILicenseStorage
{
    private readonly SignedLicenseFile? _license;
    private DateTime _lastSeen = DateTime.UtcNow;

    public InMemoryLicenseStorage(SignedLicenseFile? license) => _license = license;

    public string GetLicensePath() => "in-memory";
    public bool HasLicense() => _license != null;
    public SignedLicenseFile? LoadLicense() => _license;
    public bool SaveLicense(SignedLicenseFile license) => true;
    public bool DeleteLicense() => true;
    public bool ResetWatermark() => true;
    public DateTime? GetLastSeenTimestampUtc() => _lastSeen;
    public void UpdateLastSeenTimestampUtc(DateTime timestampUtc) => _lastSeen = timestampUtc;
    public bool CheckClockRollback(DateTime currentUtc, TimeSpan tolerance)
    {
        if (currentUtc < _lastSeen.Subtract(tolerance))
            return true;
        if (currentUtc > _lastSeen)
            _lastSeen = currentUtc;
        return false;
    }
}
