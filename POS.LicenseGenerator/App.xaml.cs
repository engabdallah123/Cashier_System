using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using POS.Licensing.Interfaces;
using POS.Licensing.Services;

namespace POS.LicenseGenerator;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length > 0)
        {
            await CliRunner.RunAsync(e.Args);
            Shutdown();
            return;
        }

        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();
        services.AddSingleton<IMachineIdProvider, MachineIdProvider>();
        services.AddSingleton<ILicenseStorage, LicenseStorage>();
        services.AddSingleton<ILicenseValidator, LicenseValidator>();

        Services = services.BuildServiceProvider();

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
