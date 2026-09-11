using System.Windows;
using System.Windows.Input;

namespace POS.Desktop
{
    public enum DraftExitChoice
    {
        SaveAndExit,
        DiscardAndExit,
        Cancel
    }

    public partial class DraftExitDialog : Window
    {
        public DraftExitChoice ResultChoice { get; private set; } = DraftExitChoice.Cancel;

        public DraftExitDialog(int itemsCount, decimal totalAmount)
        {
            InitializeComponent();
            TxtDraftSummary.Text = $"تحتوي سلة المخزون حالياً على ({itemsCount}) صنف بقيمة إجمالية {totalAmount:N2} ج.م.";

            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    ResultChoice = DraftExitChoice.Cancel;
                    Close();
                }
            };
        }

        private void OnSaveAndExitClick(object sender, RoutedEventArgs e)
        {
            ResultChoice = DraftExitChoice.SaveAndExit;
            Close();
        }

        private void OnDiscardAndExitClick(object sender, RoutedEventArgs e)
        {
            ResultChoice = DraftExitChoice.DiscardAndExit;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            ResultChoice = DraftExitChoice.Cancel;
            Close();
        }
    }
}
