using System.IO;
using POS.Licensing.Cryptography;
using POS.Licensing.Models;
using POS.Licensing.Services;

namespace POS.LicenseGenerator;

/// <summary>
/// CLI Runner for automated or terminal-based operations.
/// </summary>
public static class CliRunner
{
    private const string ProductName = "POS Supermarket Cashier System";
    private const string CurrentVersion = "1.0.0";

    public static async Task RunAsync(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        PrintHeader();

        if (args.Length > 0)
        {
            await HandleCliArgs(args);
            return;
        }

        await RunInteractiveMenu();
    }

    private static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║     POS Cashier System — License Generator Tool      ║");
        Console.WriteLine("║     VENDOR USE ONLY — DO NOT DISTRIBUTE              ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static async Task RunInteractiveMenu()
    {
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Commands:");
            Console.ResetColor();
            Console.WriteLine("  [1] Generate new RSA key pair");
            Console.WriteLine("  [2] Issue new license");
            Console.WriteLine("  [3] Verify license file");
            Console.WriteLine("  [4] Show Machine ID (current machine)");
            Console.WriteLine("  [Q] Quit");
            Console.Write("\nSelect: ");

            var choice = Console.ReadLine()?.Trim().ToUpper();
            Console.WriteLine();

            switch (choice)
            {
                case "1": await GenerateKeyPair(); break;
                case "2": await IssueLicense(); break;
                case "3": await VerifyLicense(); break;
                case "4": ShowMachineId(); break;
                case "Q": return;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Invalid option.\n");
                    Console.ResetColor();
                    break;
            }
        }
    }

    private static async Task HandleCliArgs(string[] args)
    {
        switch (args[0].ToLower())
        {
            case "auto":
                var keysDir = args.Length > 1 ? args[1] : @"E:\pos-keys";
                var machineId = args.Length > 2 ? args[2] : "POS-4C4E-76F5-CC30-CFD0";
                var customer = args.Length > 3 ? args[3] : "العميل المرخص";
                await RunAutoGeneration(keysDir, machineId, customer);
                break;
            case "keygen":
                var outDir = args.Length > 1 ? args[1] : Directory.GetCurrentDirectory();
                await GenerateKeyPair(outDir);
                break;
            case "issue":
                await IssueLicense();
                break;
            case "verify":
                var licPath = args.Length > 1 ? args[1] : null;
                await VerifyLicense(licPath);
                break;
            case "machineid":
                ShowMachineId();
                break;
            default:
                Console.WriteLine("Usage: POS.LicenseGenerator [auto [keysDir] [machineId] [customer] | keygen [outputDir] | issue | verify [licfile] | machineid]");
                break;
        }
    }

    public static async Task RunAutoGeneration(string keysDir, string machineId, string customerName)
    {
        Directory.CreateDirectory(keysDir);
        var privatePath = Path.Combine(keysDir, "license-private.pem");
        var publicPath = Path.Combine(keysDir, "license-public.pem");

        string privateKeyPem;
        string publicKeyPem;

        if (File.Exists(privatePath) && File.Exists(publicPath) && new FileInfo(privatePath).Length > 100)
        {
            privateKeyPem = await File.ReadAllTextAsync(privatePath);
            publicKeyPem = await File.ReadAllTextAsync(publicPath);
            Console.WriteLine($"[INFO] Using existing keys from {keysDir}");
        }
        else
        {
            Console.WriteLine($"[INFO] Generating new RSA-3072 key pair in {keysDir}...");
            var pair = RsaKeyHelper.GenerateKeyPair(3072);
            privateKeyPem = pair.PrivateKeyPem;
            publicKeyPem = pair.PublicKeyPem;
            await File.WriteAllTextAsync(privatePath, privateKeyPem);
            await File.WriteAllTextAsync(publicPath, publicKeyPem);
            Console.WriteLine($"[SUCCESS] Private key saved to: {privatePath}");
            Console.WriteLine($"[SUCCESS] Public key saved to: {publicPath}");
        }

        Console.WriteLine("\n=== RSA PUBLIC KEY (BEGIN) ===");
        Console.WriteLine(publicKeyPem);
        Console.WriteLine("=== RSA PUBLIC KEY (END) ===\n");

        var licenseId = Guid.NewGuid().ToString("D").ToUpperInvariant();
        var payload = new LicensePayload(
            LicenseId: licenseId,
            Product: ProductName,
            CustomerName: customerName,
            MachineId: machineId,
            LicenseType: LicenseType.Lifetime,
            IssuedAtUtc: DateTime.UtcNow,
            ExpiresAtUtc: null,
            Version: CurrentVersion,
            MinSupportedVersion: null,
            Notes: "Auto-generated Lifetime License"
        );

        var signed = LicenseSigner.Sign(payload, privateKeyPem);
        var licenseJson = CanonicalLicenseSerializer.SerializeSignedLicense(signed);

        var licFileInKeys = Path.Combine(keysDir, "license.lic");
        await File.WriteAllTextAsync(licFileInKeys, licenseJson);
        Console.WriteLine($"[SUCCESS] Generated license saved to: {licFileInKeys}");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localAppData, "POSCashier");
        Directory.CreateDirectory(appFolder);
        var activeLicFile = Path.Combine(appFolder, "license.lic");
        await File.WriteAllTextAsync(activeLicFile, licenseJson);
        Console.WriteLine($"[SUCCESS] Active license installed to: {activeLicFile}");

        var verified = LicenseSignatureVerifier.Verify(signed, publicKeyPem);
        Console.WriteLine($"[VERIFICATION] Signature Verification: {(verified ? "PASSED (VALID)" : "FAILED")}");
    }

    private static async Task GenerateKeyPair(string? outputDir = null)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("=== Generate RSA-3072 Key Pair ===");
        Console.ResetColor();

        if (string.IsNullOrWhiteSpace(outputDir))
        {
            Console.Write("Output directory for keys (e.g. E:\\pos-keys): ");
            outputDir = Console.ReadLine()?.Trim();
        }

        if (string.IsNullOrWhiteSpace(outputDir))
        {
            PrintError("Output directory cannot be empty.");
            return;
        }

        Directory.CreateDirectory(outputDir);
        var privatePath = Path.Combine(outputDir, "license-private.pem");
        var publicPath = Path.Combine(outputDir, "license-public.pem");

        if (File.Exists(privatePath))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"WARNING: '{privatePath}' already exists. Overwrite? (yes/no): ");
            Console.ResetColor();
            var confirm = Console.ReadLine()?.Trim().ToLower();
            if (confirm != "yes")
            {
                Console.WriteLine("Aborted.\n");
                return;
            }
        }

        Console.WriteLine("Generating RSA-3072 key pair...");
        var (privateKey, publicKey) = RsaKeyHelper.GenerateKeyPair(3072);

        await File.WriteAllTextAsync(privatePath, privateKey);
        await File.WriteAllTextAsync(publicPath, publicKey);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Private Key: {privatePath}");
        Console.WriteLine($"✓ Public Key:  {publicPath}");
        Console.ResetColor();

        Console.WriteLine();
        Console.WriteLine("=== PUBLIC KEY (copy to EmbeddedKeys.cs) ===");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(publicKey);
        Console.ResetColor();
        Console.WriteLine();
    }

    private static async Task IssueLicense()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("=== Issue New License ===");
        Console.ResetColor();

        var privateKey = LoadPrivateKey();
        if (string.IsNullOrWhiteSpace(privateKey))
        {
            PrintError("Could not load Private Key.");
            return;
        }

        Console.Write("Customer Name: ");
        var customerName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(customerName)) { PrintError("Customer name is required."); return; }

        Console.Write("Machine ID: ");
        var machineId = Console.ReadLine()?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(machineId)) { PrintError("Machine ID is required."); return; }

        Console.Write($"License Type [1=Trial, 2=Monthly, 3=Yearly, 4=Lifetime] (default=3): ");
        var typeInput = Console.ReadLine()?.Trim();
        var licenseType = typeInput switch
        {
            "1" => LicenseType.Trial,
            "2" => LicenseType.Monthly,
            "4" => LicenseType.Lifetime,
            _ => LicenseType.Yearly
        };

        DateTime? expiresAt = null;
        if (licenseType != LicenseType.Lifetime)
        {
            var defaultExpiry = licenseType switch
            {
                LicenseType.Trial => DateTime.UtcNow.AddDays(30),
                LicenseType.Monthly => DateTime.UtcNow.AddMonths(1),
                _ => DateTime.UtcNow.AddYears(1)
            };

            Console.Write($"Expiry Date UTC (yyyy-MM-dd) [default: {defaultExpiry:yyyy-MM-dd}]: ");
            var expiryInput = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(expiryInput))
                expiresAt = defaultExpiry;
            else if (DateTime.TryParseExact(expiryInput, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var parsedDate))
                expiresAt = parsedDate.ToUniversalTime();
            else { PrintError("Invalid date format. Use yyyy-MM-dd."); return; }
        }

        Console.Write("Output directory for license file [E:\\pos-keys]: ");
        var outDir = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(outDir)) outDir = @"E:\pos-keys";

        var licenseId = Guid.NewGuid().ToString("D").ToUpperInvariant();
        var payload = new LicensePayload(
            LicenseId: licenseId,
            Product: ProductName,
            CustomerName: customerName,
            MachineId: machineId,
            LicenseType: licenseType,
            IssuedAtUtc: DateTime.UtcNow,
            ExpiresAtUtc: expiresAt,
            Version: CurrentVersion,
            MinSupportedVersion: null,
            Notes: null
        );

        Console.WriteLine("\nSigning license...");
        var signed = LicenseSigner.Sign(payload, privateKey);

        Directory.CreateDirectory(outDir);
        var fileName = $"license-{customerName.Replace(" ", "_")}-{DateTime.UtcNow:yyyyMMdd}.lic";
        var filePath = Path.Combine(outDir, fileName);
        var json = CanonicalLicenseSerializer.SerializeSignedLicense(signed);
        await File.WriteAllTextAsync(filePath, json, System.Text.Encoding.UTF8);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n✓ License issued: {filePath}\n");
        Console.ResetColor();
    }

    private static async Task VerifyLicense(string? filePath = null)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("=== Verify License File ===");
        Console.ResetColor();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            Console.Write("License file path: ");
            filePath = Console.ReadLine()?.Trim().Trim('"');
        }

        if (!File.Exists(filePath)) { PrintError($"File not found: {filePath}"); return; }

        var json = await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8);
        var signed = CanonicalLicenseSerializer.DeserializeLicense(json);

        if (signed is null) { PrintError("Failed to parse license file."); return; }

        var publicKey = LoadPublicKey();
        if (string.IsNullOrWhiteSpace(publicKey))
        {
            PrintError("Could not load Public Key.");
            return;
        }

        var isValid = LicenseSignatureVerifier.Verify(signed, publicKey);
        Console.WriteLine($"Signature Verification: {(isValid ? "PASSED (VALID)" : "FAILED (INVALID)")}");
    }

    public static void ShowMachineId()
    {
        var provider = new MachineIdProvider();
        var id = provider.GetMachineId();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Current Machine ID: {id}");
        Console.ResetColor();
    }

    private static string? LoadPrivateKey()
    {
        const string defaultPath = @"E:\pos-keys\license-private.pem";
        if (File.Exists(defaultPath))
            return File.ReadAllText(defaultPath);

        var envPath = Environment.GetEnvironmentVariable("POS_PRIVATE_KEY_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            return File.ReadAllText(envPath);

        var envPem = Environment.GetEnvironmentVariable("POS_PRIVATE_KEY_PEM");
        if (!string.IsNullOrWhiteSpace(envPem))
            return envPem;

        Console.Write("Private Key file path: ");
        var path = Console.ReadLine()?.Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            return File.ReadAllText(path);

        return null;
    }

    private static string? LoadPublicKey()
    {
        var envPath = Environment.GetEnvironmentVariable("POS_PUBLIC_KEY_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            return File.ReadAllText(envPath);

        const string defaultPath = @"E:\pos-keys\license-public.pem";
        if (File.Exists(defaultPath))
            return File.ReadAllText(defaultPath);

        return EmbeddedKeys.PublicKeyPem;
    }

    private static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"✗ Error: {message}");
        Console.ResetColor();
    }
}
