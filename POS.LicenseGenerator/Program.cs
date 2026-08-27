using POS.Licensing.Cryptography;
using POS.Licensing.Models;
using POS.Licensing.Services;

namespace POS.LicenseGenerator;

/// <summary>
/// POS License Generator — Vendor-side tool for generating signed license files.
/// This tool must NEVER be included in the customer installer.
/// The RSA Private Key is loaded from an external file or environment variable.
/// </summary>
internal sealed class Program
{
    private const string ProductName = "POS Supermarket Cashier System";
    private const string CurrentVersion = "1.0.0";

    static async Task Main(string[] args)
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

    static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║     POS Cashier System — License Generator Tool      ║");
        Console.WriteLine("║     VENDOR USE ONLY — DO NOT DISTRIBUTE              ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    static async Task RunInteractiveMenu()
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

    static async Task HandleCliArgs(string[] args)
    {
        switch (args[0].ToLower())
        {
            case "auto":
                var keysDir = args.Length > 1 ? args[1] : @"D:\pos-keys";
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

    static async Task RunAutoGeneration(string keysDir, string machineId, string customerName)
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

        // Issue Lifetime License for this Machine ID
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

        // Save to keysDir\license.lic
        var licFileInKeys = Path.Combine(keysDir, "license.lic");
        await File.WriteAllTextAsync(licFileInKeys, licenseJson);
        Console.WriteLine($"[SUCCESS] Generated license saved to: {licFileInKeys}");

        // Save to %LocalAppData%\POSCashier\license.lic
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localAppData, "POSCashier");
        Directory.CreateDirectory(appFolder);
        var activeLicFile = Path.Combine(appFolder, "license.lic");
        await File.WriteAllTextAsync(activeLicFile, licenseJson);
        Console.WriteLine($"[SUCCESS] Active license installed to: {activeLicFile}");

        // Verify immediately
        var verified = LicenseSignatureVerifier.Verify(signed, publicKeyPem);
        Console.WriteLine($"[VERIFICATION] Signature Verification: {(verified ? "PASSED (VALID)" : "FAILED")}");
    }

    // ─── KEY GENERATION ────────────────────────────────────────────────────

    static async Task GenerateKeyPair(string? outputDir = null)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("=== Generate RSA-3072 Key Pair ===");
        Console.ResetColor();

        if (string.IsNullOrWhiteSpace(outputDir))
        {
            Console.Write("Output directory for keys (e.g. C:\\POS-Security\\Keys): ");
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

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine();
        Console.WriteLine("⚠ SECURITY NOTICE:");
        Console.WriteLine("  Keep the Private Key file SECURE and OFFLINE.");
        Console.WriteLine("  Never commit it to Git or share with anyone.");
        Console.WriteLine("  Embed the PUBLIC key in POS.Licensing/Cryptography/EmbeddedKeys.cs");
        Console.ResetColor();

        Console.WriteLine();
        Console.WriteLine("=== PUBLIC KEY (copy to EmbeddedKeys.cs) ===");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(publicKey);
        Console.ResetColor();
        Console.WriteLine();
    }

    // ─── LICENSE ISSUANCE ──────────────────────────────────────────────────

    static async Task IssueLicense()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("=== Issue New License ===");
        Console.ResetColor();

        // Load private key
        var privateKey = LoadPrivateKey();
        if (string.IsNullOrWhiteSpace(privateKey))
        {
            PrintError("Could not load Private Key. Set POS_PRIVATE_KEY_PATH environment variable or provide path.");
            return;
        }

        // Gather inputs
        Console.Write("Customer Name: ");
        var customerName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(customerName)) { PrintError("Customer name is required."); return; }

        Console.Write("Machine ID (from customer's activation screen): ");
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

        Console.Write($"Product [{ProductName}]: ");
        var product = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(product)) product = ProductName;

        Console.Write($"Version [{CurrentVersion}]: ");
        var version = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(version)) version = CurrentVersion;

        Console.Write("Min Supported Version (optional, leave blank to skip): ");
        var minVersion = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(minVersion)) minVersion = null;

        Console.Write("Notes (optional): ");
        var notes = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(notes)) notes = null;

        Console.Write("Output directory for license file [current dir]: ");
        var outDir = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(outDir)) outDir = Directory.GetCurrentDirectory();

        // Build payload
        var licenseId = Guid.NewGuid().ToString("D").ToUpperInvariant();
        var payload = new LicensePayload(
            LicenseId: licenseId,
            Product: product,
            CustomerName: customerName,
            MachineId: machineId,
            LicenseType: licenseType,
            IssuedAtUtc: DateTime.UtcNow,
            ExpiresAtUtc: expiresAt,
            Version: version,
            MinSupportedVersion: minVersion,
            Notes: notes
        );

        // Sign
        Console.WriteLine("\nSigning license...");
        var signed = LicenseSigner.Sign(payload, privateKey);

        // Save
        Directory.CreateDirectory(outDir);
        var fileName = $"license-{customerName.Replace(" ", "_")}-{DateTime.UtcNow:yyyyMMdd}.lic";
        var filePath = Path.Combine(outDir, fileName);
        var json = CanonicalLicenseSerializer.SerializeSignedLicense(signed);
        await File.WriteAllTextAsync(filePath, json, System.Text.Encoding.UTF8);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n✓ License issued successfully:");
        Console.ResetColor();
        Console.WriteLine($"  License ID   : {licenseId}");
        Console.WriteLine($"  Customer     : {customerName}");
        Console.WriteLine($"  Machine ID   : {machineId}");
        Console.WriteLine($"  Type         : {licenseType}");
        Console.WriteLine($"  Expires      : {(expiresAt.HasValue ? expiresAt.Value.ToString("yyyy-MM-dd") : "Never (Lifetime)")}");
        Console.WriteLine($"  Output File  : {filePath}");
        Console.WriteLine();
    }

    // ─── VERIFY LICENSE ────────────────────────────────────────────────────

    static async Task VerifyLicense(string? filePath = null)
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
        if (isValid)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Signature is VALID");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("✗ Signature is INVALID or file has been tampered with!");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine($"  License ID   : {signed.Payload?.LicenseId}");
        Console.WriteLine($"  Customer     : {signed.Payload?.CustomerName}");
        Console.WriteLine($"  Product      : {signed.Payload?.Product}");
        Console.WriteLine($"  Machine ID   : {signed.Payload?.MachineId}");
        Console.WriteLine($"  Type         : {signed.Payload?.LicenseType}");
        Console.WriteLine($"  Issued       : {signed.Payload?.IssuedAtUtc:yyyy-MM-dd}");
        Console.WriteLine($"  Expires      : {(signed.Payload?.ExpiresAtUtc.HasValue == true ? signed.Payload.ExpiresAtUtc.Value.ToString("yyyy-MM-dd") : "Never (Lifetime)")}");
        Console.WriteLine();
    }

    // ─── MACHINE ID ────────────────────────────────────────────────────────

    static void ShowMachineId()
    {
        var provider = new MachineIdProvider();
        var id = provider.GetMachineId();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Current Machine ID: {id}");
        Console.ResetColor();
        Console.WriteLine();
    }

    // ─── HELPERS ───────────────────────────────────────────────────────────

    static string? LoadPrivateKey()
    {
        // 1. Try environment variable path
        var envPath = Environment.GetEnvironmentVariable("POS_PRIVATE_KEY_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            return File.ReadAllText(envPath);

        // 2. Try environment variable raw PEM content
        var envPem = Environment.GetEnvironmentVariable("POS_PRIVATE_KEY_PEM");
        if (!string.IsNullOrWhiteSpace(envPem))
            return envPem;

        // 3. Prompt user
        Console.Write("Private Key file path: ");
        var path = Console.ReadLine()?.Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            return File.ReadAllText(path);

        return null;
    }

    static string? LoadPublicKey()
    {
        var envPath = Environment.GetEnvironmentVariable("POS_PUBLIC_KEY_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            return File.ReadAllText(envPath);

        // Fall back to embedded key
        return EmbeddedKeys.PublicKeyPem;
    }

    static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"✗ Error: {message}");
        Console.ResetColor();
        Console.WriteLine();
    }
}
