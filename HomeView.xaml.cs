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

            if (!DashboardManager.TryGetSnapshot(selectedYear, out DashboardSnapshot snapshot, out string errorMessage))
            {
                SetFinancialValuesUnavailable();
                BalanceErrorText.Text = "تعذر قراءة الأرصدة من قاعدة البيانات. تأكد من ملف قاعدة البيانات ثم أعد فتح الصفحة.";
                BalanceErrorText.ToolTip = errorMessage;
                BalanceErrorText.Visibility = Visibility.Visible;
                return;
            }

            BalanceErrorText.Visibility = Visibility.Collapsed;
            BalanceErrorText.ToolTip = null;

            RevenuesBalanceCardTitle.Text = "رصيد الإيرادات";
            TotalRevenuesText.Text = snapshot.RevenuesBalance.ToString("N3");

            InvoicesBalanceCardTitle.Text = "رصيد صندوق الفواتير";
            TotalExpensesText.Text = snapshot.InvoicesBalance.ToString("N3");

            FundBalanceText.Text = snapshot.FundBalance.ToString("N3");
            TotalReceiptsSummaryText.Text = snapshot.TotalReceipts.ToString("N3");
            TotalDepositsSummaryText.Text = snapshot.TotalDeposits.ToString("N3");
            FundAdditionsSummaryText.Text = snapshot.FundAdditions.ToString("N3");
            InvoiceExpensesSummaryText.Text = snapshot.InvoiceExpenses.ToString("N3");
            AidsSummaryText.Text = snapshot.TotalAids.ToString("N3");
            FuelSummaryText.Text = snapshot.TotalFuel.ToString("N3");
            UnpaidFuelSummaryText.Text = snapshot.UnpaidFuel.ToString("N3");
        }

        private void SetFinancialValuesUnavailable()
        {
            TotalRevenuesText.Text = "—";
            TotalExpensesText.Text = "—";
            FundBalanceText.Text = "—";
            TotalReceiptsSummaryText.Text = "—";
            TotalDepositsSummaryText.Text = "—";
            FundAdditionsSummaryText.Text = "—";
            InvoiceExpensesSummaryText.Text = "—";
            AidsSummaryText.Text = "—";
            FuelSummaryText.Text = "—";
            UnpaidFuelSummaryText.Text = "—";
        }
    }
}
