using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using POS.Licensing.Models;

namespace POS.Licensing.Cryptography;

/// <summary>
/// Provides deterministic canonical JSON serialization for LicensePayload.
/// Ensures identical UTF-8 byte stream during signing and verification across platforms.
/// </summary>
public static class CanonicalLicenseSerializer
{
    private static readonly UTF8Encoding Utf8Encoding = new(false);

    public static byte[] SerializeToCanonicalBytes(LicensePayload payload)
    {
        var canonicalJson = SerializeToCanonicalJson(payload);
        return Utf8Encoding.GetBytes(canonicalJson);
    }

    public static string SerializeToCanonicalJson(LicensePayload payload)
    {
        if (payload is null)
            throw new ArgumentNullException(nameof(payload));

        // Construct sorted, canonical dictionary with invariant representations
        var sorted = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["customerName"] = payload.CustomerName?.Trim() ?? string.Empty,
            ["expiresAtUtc"] = payload.ExpiresAtUtc.HasValue 
                ? payload.ExpiresAtUtc.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
                : null,
            ["issuedAtUtc"] = payload.IssuedAtUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ["licenseId"] = payload.LicenseId?.Trim() ?? string.Empty,
            ["licenseType"] = payload.LicenseType.ToString(),
            ["machineId"] = payload.MachineId?.Trim().ToUpperInvariant() ?? string.Empty,
            ["maxSupportedVersion"] = string.IsNullOrWhiteSpace(payload.MaxSupportedVersion) ? null : payload.MaxSupportedVersion.Trim(),
            ["minSupportedVersion"] = string.IsNullOrWhiteSpace(payload.MinSupportedVersion) ? null : payload.MinSupportedVersion.Trim(),
            ["notes"] = string.IsNullOrWhiteSpace(payload.Notes) ? null : payload.Notes.Trim(),
            ["product"] = payload.Product?.Trim() ?? string.Empty,
            ["version"] = payload.Version?.Trim() ?? string.Empty
        };

        if (payload.Features != null && payload.Features.Count > 0)
        {
            var sortedFeatures = new SortedDictionary<string, string>(payload.Features, StringComparer.Ordinal);
            sorted["features"] = sortedFeatures;
        }
        else
        {
            sorted["features"] = null;
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        return JsonSerializer.Serialize(sorted, options);
    }

    public static SignedLicenseFile? DeserializeLicense(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
            return null;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<SignedLicenseFile>(jsonContent, options);
    }

    public static string SerializeSignedLicense(SignedLicenseFile signedLicense)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        return JsonSerializer.Serialize(signedLicense, options);
    }
}
