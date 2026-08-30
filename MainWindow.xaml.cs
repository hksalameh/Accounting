using System.Windows;

namespace AccountingApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ContentArea.Child = new HomeView();
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            ContentArea.Child = new HomeView();
        }

        private void Invoices_Click(object sender, RoutedEventArgs e)
        {
            ContentArea.Child = new InvoicesView();
        }

        private void Revenues_Click(object sender, RoutedEventArgs e)
        {
            ContentArea.Child = new RevenuesView();
        }

        private void Aids_Click(object sender, RoutedEventArgs e)
        {
            ContentArea.Child = new AidsView();
        }

        private void Fuel_Click(object sender, RoutedEventArgs e)
        {
            ContentArea.Child = new FuelView();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
