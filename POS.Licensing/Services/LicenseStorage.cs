using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using POS.Licensing.Cryptography;
using POS.Licensing.Interfaces;
using POS.Licensing.Models;

namespace POS.Licensing.Services;

/// <summary>
/// Handles local persistence of license file and timestamp watermark in %LocalAppData%\POSCashier.
/// </summary>
public sealed class LicenseStorage : ILicenseStorage
{
    private const string AppFolderName = "POSCashier";
    private const string LicenseFileName = "license.lic";
    private const string WatermarkFileName = ".watermark";

    private readonly string _storageDirectory;
    private readonly string _licenseFilePath;
    private readonly string _watermarkFilePath;
    private readonly object _fileLock = new();

    public LicenseStorage(string? customDirectory = null)
    {
        _storageDirectory = string.IsNullOrWhiteSpace(customDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName)
            : customDirectory;

        _licenseFilePath = Path.Combine(_storageDirectory, LicenseFileName);
        _watermarkFilePath = Path.Combine(_storageDirectory, WatermarkFileName);

        EnsureDirectoryExists();
    }

    public string GetLicensePath() => _licenseFilePath;

    public bool HasLicense() => File.Exists(_licenseFilePath);

    public SignedLicenseFile? LoadLicense()
    {
        lock (_fileLock)
        {
            if (!File.Exists(_licenseFilePath))
                return null;

            try
            {
                var json = File.ReadAllText(_licenseFilePath, Encoding.UTF8);
                return CanonicalLicenseSerializer.DeserializeLicense(json);
            }
            catch
            {
                return null;
            }
        }
    }

    public bool SaveLicense(SignedLicenseFile license)
    {
        if (license is null)
            return false;

        lock (_fileLock)
        {
            try
            {
                EnsureDirectoryExists();
                var json = CanonicalLicenseSerializer.SerializeSignedLicense(license);
                File.WriteAllText(_licenseFilePath, json, Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool DeleteLicense()
    {
        lock (_fileLock)
        {
            try
            {
                if (File.Exists(_licenseFilePath))
                    File.Delete(_licenseFilePath);

                if (File.Exists(_watermarkFilePath))
                    File.Delete(_watermarkFilePath);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool ResetWatermark()
    {
        lock (_fileLock)
        {
            try
            {
                if (File.Exists(_watermarkFilePath))
                    File.Delete(_watermarkFilePath);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public DateTime? GetLastSeenTimestampUtc()
    {
        lock (_fileLock)
        {
            if (!File.Exists(_watermarkFilePath))
                return null;

            try
            {
                var base64 = File.ReadAllText(_watermarkFilePath, Encoding.UTF8).Trim();
                var bytes = Convert.FromBase64String(base64);
                var rawString = Encoding.UTF8.GetString(bytes);

                if (DateTime.TryParseExact(rawString, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt))
                {
                    return dt;
                }
            }
            catch
            {
                // Ignored on corrupted watermark
            }

            return null;
        }
    }

    public void UpdateLastSeenTimestampUtc(DateTime timestampUtc)
    {
        lock (_fileLock)
        {
            try
            {
                EnsureDirectoryExists();
                var dateStr = timestampUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
                var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(dateStr));
                File.WriteAllText(_watermarkFilePath, base64, Encoding.UTF8);
            }
            catch
            {
                // Silently handle transient I/O exceptions
            }
        }
    }

    public bool CheckClockRollback(DateTime currentUtc, TimeSpan tolerance)
    {
        var lastSeenUtc = GetLastSeenTimestampUtc();
        if (!lastSeenUtc.HasValue)
        {
            // First run, record current time
            UpdateLastSeenTimestampUtc(currentUtc);
            return false;
        }

        // If current time is significantly before last recorded time, rollback is detected
        if (currentUtc < lastSeenUtc.Value.Subtract(tolerance))
        {
            return true; // Rollback detected!
        }

        // Advance watermark if current time is newer
        if (currentUtc > lastSeenUtc.Value)
        {
            UpdateLastSeenTimestampUtc(currentUtc);
        }

        return false;
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }
}
