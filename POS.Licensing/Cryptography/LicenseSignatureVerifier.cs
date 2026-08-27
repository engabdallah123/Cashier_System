using System.Security.Cryptography;
using POS.Licensing.Models;

namespace POS.Licensing.Cryptography;

/// <summary>
/// Verifies digital signatures of licenses using RSA Public Key.
/// </summary>
public static class LicenseSignatureVerifier
{
    public static bool Verify(SignedLicenseFile signedLicense, string publicKeyPem)
    {
        if (signedLicense?.Payload is null || string.IsNullOrWhiteSpace(signedLicense.Signature))
            return false;

        if (string.IsNullOrWhiteSpace(publicKeyPem))
            throw new ArgumentException("Public Key PEM cannot be null or empty.", nameof(publicKeyPem));

        try
        {
            var canonicalBytes = CanonicalLicenseSerializer.SerializeToCanonicalBytes(signedLicense.Payload);
            var signatureBytes = Convert.FromBase64String(signedLicense.Signature);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem.Trim());

            return rsa.VerifyData(canonicalBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }
}
