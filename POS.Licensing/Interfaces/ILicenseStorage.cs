using POS.Licensing.Models;

namespace POS.Licensing.Interfaces;

public interface ILicenseStorage
{
    string GetLicensePath();
    SignedLicenseFile? LoadLicense();
    bool SaveLicense(SignedLicenseFile license);
    bool DeleteLicense();
    bool HasLicense();
    
    DateTime? GetLastSeenTimestampUtc();
    void UpdateLastSeenTimestampUtc(DateTime timestampUtc);
    bool CheckClockRollback(DateTime currentUtc, TimeSpan tolerance);
}
