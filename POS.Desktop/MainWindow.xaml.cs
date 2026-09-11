using Microsoft.AspNetCore.Components.WebView.Wpf;
using System.Windows;

namespace POS.Desktop
{
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
                    // Disable built-in Chromium browser shortcut keys (Ctrl+P print, Ctrl+S save, Ctrl+F find, etc.)
                    blazorWebView.WebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                }
            };

            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                var draftContainer = App.Services?.GetService(typeof(POS.Desktop.Services.State.PurchaseDraftStateContainer)) 
                    as POS.Desktop.Services.State.PurchaseDraftStateContainer;

                if (draftContainer != null && draftContainer.HasDraft)
                {
                    var dialog = new DraftExitDialog(draftContainer.ItemsCount, draftContainer.TotalAmount)
                    {
                        Owner = this
                    };
                    dialog.ShowDialog();

                    switch (dialog.ResultChoice)
                    {
                        case DraftExitChoice.SaveAndExit:
                            draftContainer.SaveToDisk();
                            e.Cancel = false;
                            break;

                        case DraftExitChoice.DiscardAndExit:
                            draftContainer.ClearDraft();
                            e.Cancel = false;
                            break;

                        case DraftExitChoice.Cancel:
                        default:
                            e.Cancel = true;
                            break;
                    }
                    return;
                }

                var cartContainer = App.Services?.GetService(typeof(POS.Desktop.Services.State.CartStateContainer))
                    as POS.Desktop.Services.State.CartStateContainer;

                if (cartContainer != null && cartContainer.Items.Any())
                {
                    var msg = $"توجد منتجات ({cartContainer.Items.Count}) في سلة المبيعات لم يتم إتمام بيعها بعد.\n\nهل تريد الخروج وتجاهل السلة أم البقاء في البرنامج؟";
                    var res = MessageBox.Show(msg, "تنبيه قبل الخروج", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                    if (res != MessageBoxResult.Yes)
                    {
                        e.Cancel = true;
                    }
                    return;
                }

                // General Exit Confirmation
                var confirmResult = MessageBox.Show(
                    "هل تريد الخروج من البرنامج؟",
                    "تأكيد الخروج",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes,
                    MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

                if (confirmResult != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                }
            }
            catch
            {
                // In case of any unexpected dialog exception, do not crash the app
            }
        }
    }
}
