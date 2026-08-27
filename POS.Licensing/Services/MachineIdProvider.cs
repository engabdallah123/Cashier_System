using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using POS.Licensing.Interfaces;

namespace POS.Licensing.Services;

/// <summary>
/// Provides stable and normalized machine binding identifier for Windows.
/// Combines Windows MachineGuid and hardware identifiers with graceful fallback strategies.
/// </summary>
public sealed class MachineIdProvider : IMachineIdProvider
{
    private string? _cachedMachineId;
    private readonly object _lock = new();

    public string GetMachineId()
    {
        if (_cachedMachineId != null)
            return _cachedMachineId;

        lock (_lock)
        {
            if (_cachedMachineId != null)
                return _cachedMachineId;

            _cachedMachineId = ComputeMachineId();
            return _cachedMachineId;
        }
    }

    public bool ValidateMachineId(string expectedMachineId)
    {
        if (string.IsNullOrWhiteSpace(expectedMachineId))
            return false;

        var currentId = GetMachineId();
        return string.Equals(currentId.Trim(), expectedMachineId.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeMachineId()
    {
        var rawComponents = new StringBuilder();

        // 1. Windows MachineGuid (Registry - highly reliable and stable across reboots)
        var machineGuid = GetWindowsMachineGuid();
        if (!string.IsNullOrWhiteSpace(machineGuid))
        {
            rawComponents.Append("GUID:").Append(machineGuid).Append(';');
        }

        // 2. Motherboard / BIOS Serial Number
        var motherBoardSerial = GetMotherboardSerialNumber();
        if (!string.IsNullOrWhiteSpace(motherBoardSerial))
        {
            rawComponents.Append("MB:").Append(motherBoardSerial).Append(';');
        }

        // 3. Processor Identifier / Architecture
        var processorId = GetProcessorIdentifier();
        if (!string.IsNullOrWhiteSpace(processorId))
        {
            rawComponents.Append("CPU:").Append(processorId).Append(';');
        }

        // Fallback: If hardware queries were restricted, use machine name & OS details
        if (rawComponents.Length == 0)
        {
            rawComponents.Append("ENV:").Append(Environment.MachineName).Append('-').Append(Environment.UserName);
        }

        // Hash components with SHA-256
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawComponents.ToString()));
        var hex = Convert.ToHexString(hashBytes).ToUpperInvariant();

        // Format into readable: POS-XXXX-XXXX-XXXX-XXXX (16 chars from hash)
        return $"POS-{hex[..4]}-{hex.Substring(4, 4)}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}";
    }

    private static string? GetWindowsMachineGuid()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                                          .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                return key?.GetValue("MachineGuid")?.ToString();
            }
        }
        catch
        {
            // Graceful fallback on permission error
        }

        return null;
    }

    private static string? GetMotherboardSerialNumber()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
            foreach (var obj in searcher.Get())
            {
                var serial = obj["SerialNumber"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(serial) && !serial.Equals("None", StringComparison.OrdinalIgnoreCase) && !serial.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase))
                {
                    return serial;
                }
            }
        }
        catch
        {
            // Fallback if WMI service is disabled or query fails
        }

        return null;
    }

    private static string? GetProcessorIdentifier()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Environment.ProcessorCount.ToString();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                var procId = obj["ProcessorId"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(procId))
                {
                    return procId;
                }
            }
        }
        catch
        {
            // Fallback to Environment
        }

        return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
    }
}
