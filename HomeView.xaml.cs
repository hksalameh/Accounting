using System.Windows;
using System.Windows.Controls;

namespace AccountingApp
{
    public partial class HomeView : UserControl
    {
        public HomeView()
        {
            InitializeComponent();
        }

        private void HomeView_Loaded(object sender, RoutedEventArgs e)
        {
            FiscalYearHelper.SelectCurrentYear(YearComboBox);
            RefreshBalances();
        }

        private void YearComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                RefreshBalances();
            }
        }

        private void RefreshBalances()
        {
            if (YearComboBox.SelectedItem == null) return;

            int selectedYear = int.Parse(YearComboBox.SelectedItem.ToString());

            if (!DashboardManager.TryGetBalances(
                selectedYear,
                out decimal revenuesBalance,
                out decimal invoicesBalance,
                out string errorMessage))
            {
                // عند فشل القراءة لا نعرض صفراً لأنه قد يُفهم على أنه رصيد حقيقي.
                TotalRevenuesText.Text = "—";
                TotalExpensesText.Text = "—";
                FundBalanceText.Text = "—";
                BalanceErrorText.Text = "تعذر قراءة الأرصدة من قاعدة البيانات. تأكد من ملف قاعدة البيانات ثم أعد فتح الصفحة.";
                BalanceErrorText.ToolTip = errorMessage;
                BalanceErrorText.Visibility = Visibility.Visible;
                return;
            }

            BalanceErrorText.Visibility = Visibility.Collapsed;
            BalanceErrorText.ToolTip = null;

            // الرصيد العام للصندوق يجمع رصيد الإيرادات مع رصيد صندوق الفواتير.
            decimal fundBalance = revenuesBalance + invoicesBalance;

            RevenuesBalanceCardTitle.Text = "رصيد الإيرادات";
            TotalRevenuesText.Text = revenuesBalance.ToString("N3");

            InvoicesBalanceCardTitle.Text = "رصيد صندوق الفواتير";
            TotalExpensesText.Text = invoicesBalance.ToString("N3");

            FundBalanceText.Text = fundBalance.ToString("N3");
        }
    }
}
