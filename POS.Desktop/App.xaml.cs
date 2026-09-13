using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using POS.Desktop.Services.Api;
using POS.Desktop.Services.Auth;
using POS.Desktop.Services.Printing;
using POS.Desktop.Services.State;
using POS.Licensing.Interfaces;
using POS.Licensing.Services;
using System.Diagnostics;
using System.IO;      
using System.Net.Http;
using System.Windows;

namespace POS.Desktop
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;
        private static Process? _apiProcess;

        protected override void OnStartup(StartupEventArgs e)
        {
            var culture = new System.Globalization.CultureInfo("en-GB");
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;

            // Enforce en-GB locale in underlying Chromium/WebView2 process so HTML5 date controls use dd/MM/yyyy
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", "--lang=en-GB");

            base.OnStartup(e);

            EnsureBackendApiRunning();
            WaitForBackendReady();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddWpfBlazorWebView();

            // Licensing Services
            serviceCollection.AddSingleton<IMachineIdProvider, MachineIdProvider>();
            serviceCollection.AddSingleton<ILicenseStorage, LicenseStorage>();
            serviceCollection.AddSingleton<ILicenseValidator, LicenseValidator>();
            serviceCollection.AddSingleton<LicenseStateContainer>();

            serviceCollection.AddSingleton<CustomAuthStateProvider>();
            serviceCollection.AddSingleton<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthStateProvider>());
            serviceCollection.AddAuthorizationCore();

            serviceCollection.AddTransient<BearerTokenHandler>();

            var createHandler = () => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            var baseApiUri = new Uri("http://localhost:5000/");

            // Register untyped HttpClient for pages using @inject HttpClient
            serviceCollection.AddHttpClient("", client =>
            {
                client.BaseAddress = baseApiUri;
                client.Timeout = TimeSpan.FromMinutes(15);
            })
            .ConfigurePrimaryHttpMessageHandler(createHandler)
            .AddHttpMessageHandler<BearerTokenHandler>();

            serviceCollection.AddHttpClient<PosApiClient>(client =>
            {
                client.BaseAddress = baseApiUri;
                client.Timeout = TimeSpan.FromMinutes(15);
            })
            .ConfigurePrimaryHttpMessageHandler(createHandler)
            .AddHttpMessageHandler<BearerTokenHandler>();

            serviceCollection.AddHttpClient<IInvoicePrinterService, QuestPdfInvoicePrinter>(client =>
            {
                client.BaseAddress = baseApiUri;
                client.Timeout = TimeSpan.FromMinutes(15);
            })
            .ConfigurePrimaryHttpMessageHandler(createHandler)
            .AddHttpMessageHandler<BearerTokenHandler>();

            serviceCollection.AddSingleton<ShiftStateContainer>();
            serviceCollection.AddSingleton<CartStateContainer>();
            serviceCollection.AddSingleton<PurchaseDraftStateContainer>();
            serviceCollection.AddSingleton<StoreStateContainer>();
            serviceCollection.AddSingleton<CalculatorStateContainer>();

            Services = serviceCollection.BuildServiceProvider();
        }

        private static void EnsureBackendApiRunning()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(1500) };
                var response = client.GetAsync("http://localhost:5000/health").GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode)
                {
                    return; // Backend is already running
                }
            }
            catch
            {
                // Not running, try to launch local WebAPI process
            }

            EnsureDatabaseServiceRunning();

            try
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var possiblePaths = new[]
                {
                    Path.Combine(appDir, "..", "WebAPI", "POS.WebAPI.exe"),
                    Path.Combine(appDir, "..", "webapi", "POS.WebAPI.exe"),
                    Path.Combine(appDir, "WebAPI", "POS.WebAPI.exe"),
                    Path.Combine(appDir, "webapi", "POS.WebAPI.exe"),
                    Path.Combine(appDir, "POS.WebAPI.exe"),
                    Path.Combine(appDir, "..", "..", "..", "..", "POS.WebAPI", "bin", "Debug", "net10.0", "POS.WebAPI.exe"),
                    Path.Combine(appDir, "..", "..", "..", "..", "POS.WebAPI", "bin", "Release", "net10.0", "POS.WebAPI.exe"),
                    Path.Combine(appDir, "..", "..", "..", "..", "publish", "webapi", "POS.WebAPI.exe")
                };

                foreach (var path in possiblePaths)
                {
                    var fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "POSCashier");
                        Directory.CreateDirectory(logDir);

                        var startInfo = new ProcessStartInfo
                        {
                            FileName = fullPath,
                            WorkingDirectory = Path.GetDirectoryName(fullPath),
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            RedirectStandardOutput = false,
                            RedirectStandardError = false
                        };
                        _apiProcess = Process.Start(startInfo);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "POSCashier", "startup_error.log");
                    File.WriteAllText(logPath, $"[API Startup Error] {ex}");
                }
                catch { }
            }
        }

        private static void EnsureDatabaseServiceRunning()
        {
            try
            {
                // 1. Try starting SQL Server Express service if it exists and is stopped
                var psiSqlExpress = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = "start MSSQL$SQLEXPRESS",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var proc = Process.Start(psiSqlExpress))
                {
                    proc?.WaitForExit(3000);
                }
            }
            catch
            {
                // Ignore errors if sc.exe is not accessible or service doesn't exist
            }

            try
            {
                // 2. Fallback: also try LocalDB in case system is running with LocalDB
                var psiStart = new ProcessStartInfo
                {
                    FileName = "sqllocaldb",
                    Arguments = "start MSSQLLocalDB",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var procStart = Process.Start(psiStart))
                {
                    procStart?.WaitForExit(3000);
                }
            }
            catch
            {
                // Fallback silently if sqllocaldb CLI is unavailable
            }
        }

        private static void WaitForBackendReady()
        {
            // Migrations run when the API starts.  Do not let the login page
            // query it while the local database is still being prepared.
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var deadline = DateTime.UtcNow.AddSeconds(30);

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var response = client.GetAsync("http://localhost:5000/health").GetAwaiter().GetResult();
                    if (response.IsSuccessStatusCode)
                    {
                        return;
                    }
                }
                catch
                {
                    // The API process or LocalDB is still starting.
                }

                Thread.Sleep(500);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                if (_apiProcess != null && !_apiProcess.HasExited)
                {
                    _apiProcess.Kill();
                    _apiProcess.Dispose();
                }
            }
            catch { }

            base.OnExit(e);
        }
    }
}
