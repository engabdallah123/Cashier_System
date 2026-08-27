using System.Security.Cryptography;
using POS.Licensing.Models;

namespace POS.Licensing.Cryptography;

/// <summary>
/// Used exclusively on vendor/developer side and in tests to sign license payloads with RSA Private Key.
/// </summary>
public static class LicenseSigner
{
    public static SignedLicenseFile Sign(LicensePayload payload, string privateKeyPem)
    {
        if (payload is null)
            throw new ArgumentNullException(nameof(payload));

        if (string.IsNullOrWhiteSpace(privateKeyPem))
            throw new ArgumentException("Private Key PEM cannot be null or empty.", nameof(privateKeyPem));

        var canonicalBytes = CanonicalLicenseSerializer.SerializeToCanonicalBytes(payload);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem.Trim());

        var signatureBytes = rsa.SignData(canonicalBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signatureBase64 = Convert.ToBase64String(signatureBytes);

        return new SignedLicenseFile(payload, signatureBase64);
    }
}
