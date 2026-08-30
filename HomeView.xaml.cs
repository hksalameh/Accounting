using System;
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

            decimal revenuesBalance = DashboardManager.GetRevenuesBalance(selectedYear);
            decimal invoicesBalance = DashboardManager.GetInvoicesBalance(selectedYear);

            // *** التعديل هنا: المعادلة الجديدة لرصيد الصندوق ***
            decimal fundBalance = revenuesBalance + invoicesBalance;

            RevenuesBalanceCardTitle.Text = "رصيد الإيرادات";
            TotalRevenuesText.Text = revenuesBalance.ToString("N3");

            InvoicesBalanceCardTitle.Text = "رصيد الفواتير";
            TotalExpensesText.Text = invoicesBalance.ToString("N3");

            FundBalanceText.Text = fundBalance.ToString("N3");
        }
    }
}
