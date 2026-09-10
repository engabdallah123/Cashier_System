using System.Windows;
using Microsoft.AspNetCore.Components.WebView.Wpf;

namespace POS.LicenseGenerator;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        blazorWebView.Services = App.Services;
        blazorWebView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(AppRoutes)
        });

        blazorWebView.BlazorWebViewInitialized += (sender, e) =>
        {
            if (blazorWebView.WebView?.CoreWebView2?.Settings != null)
            {
                blazorWebView.WebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            }
        };
    }
}
