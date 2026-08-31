using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Printing;

namespace AccountingApp
{
    public partial class FuelView : UserControl
    {
        private readonly string _connectionString = DatabaseService.ConnectionString;
        private FuelInvoice _invoiceToEdit = null;

        public FuelView()
        {
            InitializeComponent();
            DatabaseService.InitializeDatabase();
        }

        #region Load and Refresh
        private void FuelView_Loaded(object sender, RoutedEventArgs e)
        {
            FiscalYearHelper.SelectCurrentYear(YearComboBox);
            ResetDateFieldsToSelectedYear();
            RefreshAllData();
        }

        private void RefreshAllData()
        {
            PopulateCarComboBoxes();
            RefreshFuelInvoicesGrid();
            RefreshCarsGrid();
        }

        private void PopulateCarComboBoxes()
        {
            var cars = LoadCars().Select(c => c.CarNumber).ToList();
            var searchCarList = new List<string> { "الكل" };
            searchCarList.AddRange(cars);

            string currentSearch = SearchCarComboBox.SelectedItem as string;
            SearchCarComboBox.ItemsSource = searchCarList;
            SearchCarComboBox.SelectedItem = !string.IsNullOrWhiteSpace(currentSearch) && searchCarList.Contains(currentSearch)
                ? currentSearch
                : "الكل";

            string currentInvoiceCar = InvoiceCarComboBox.SelectedItem as string;
            InvoiceCarComboBox.ItemsSource = cars;
            if (!string.IsNullOrWhiteSpace(currentInvoiceCar) && cars.Contains(currentInvoiceCar))
            {
                InvoiceCarComboBox.SelectedItem = currentInvoiceCar;
            }

            string currentSummaryCar = SummaryCarComboBox.SelectedItem as string;
            SummaryCarComboBox.ItemsSource = searchCarList;
            SummaryCarComboBox.SelectedItem = !string.IsNullOrWhiteSpace(currentSummaryCar) && searchCarList.Contains(currentSummaryCar)
                ? currentSummaryCar
                : "الكل";
        }

        private void RefreshFuelInvoicesGrid()
        {
            if (YearComboBox.SelectedItem == null) return;

            if (!TryParseOptionalDate(SearchFromDateTextBox.Text, "تاريخ بداية البحث", out DateTime? fromDate)) return;
            if (!TryParseOptionalDate(SearchToDateTextBox.Text, "تاريخ نهاية البحث", out DateTime? toDate)) return;

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            {
                MessageBox.Show(
                    "تاريخ بداية البحث يجب أن يكون قبل أو مساوياً لتاريخ النهاية.",
                    "نطاق تاريخ غير صحيح",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string selectedCar = SearchCarComboBox.SelectedItem as string;
            if (selectedCar == "الكل") selectedCar = null;

            bool unpaidOnly = ShowUnpaidOnlyCheckBox.IsChecked ?? false;
            var invoices = LoadFuelInvoices(selectedCar, fromDate, toDate, unpaidOnly);

            FuelInvoicesDataGrid.ItemsSource = null;
            FuelInvoicesDataGrid.ItemsSource = invoices;
        }

        private void RefreshCarsGrid()
        {
            CarsDataGrid.ItemsSource = LoadCars();
        }

        private void RefreshData_Event(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;

            if (sender == YearComboBox)
            {
                ClearFields_Click(null, null);
                ResetDateFieldsToSelectedYear();
                TotalAmountTextBlock.Text = "0.000";
            }

            RefreshFuelInvoicesGrid();
        }

        private void ShowAll_Click(object sender, RoutedEventArgs e)
        {
            SearchCarComboBox.SelectedIndex = 0;
            ResetDateFieldsToSelectedYear();
            ShowUnpaidOnlyCheckBox.IsChecked = false;
            RefreshFuelInvoicesGrid();
        }
        #endregion

        #region Car Management
        private void AddCar_Click(object sender, RoutedEventArgs e)
        {
            string newCarNumber = (NewCarNumberTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(newCarNumber))
            {
                MessageBox.Show("الرجاء إدخال رقم السيارة.");
                return;
            }

            if (SaveCar(new Car { CarNumber = newCarNumber }))
            {
                MessageBox.Show("تمت إضافة السيارة بنجاح.");
                NewCarNumberTextBox.Clear();
                RefreshAllData();
            }
            else
            {
                MessageBox.Show("هذه السيارة موجودة بالفعل.");
            }
        }

        private void DeleteCar_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as Button)?.DataContext is Car selectedCar)) return;

            if (HasFuelInvoices(selectedCar.CarNumber))
            {
                MessageBox.Show(
                    $"لا يمكن حذف السيارة {selectedCar.CarNumber} لأنها مرتبطة بفواتير وقود محفوظة.\n\nتم منع الحذف لحماية السجل التاريخي.",
                    "لا يمكن حذف السيارة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show(
                $"هل أنت متأكد من حذف السيارة {selectedCar.CarNumber}؟",
                "تأكيد الحذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                DeleteCar(selectedCar.Id);
                RefreshAllData();
            }
        }
        #endregion

        #region Invoice Management
        private void AddUpdateInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (InvoiceCarComboBox.SelectedItem == null)
            {
                MessageBox.Show("الرجاء اختيار السيارة.");
                return;
            }

            if (!TryParseDate(InvoiceDateTextBox.Text, out DateTime date, true)) return;
            if (!FiscalYearHelper.ValidateDateInSelectedYear(date, YearComboBox, "تاريخ فاتورة الوقود")) return;

            if (!decimal.TryParse(AmountTextBox.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("الرجاء إدخال مبلغ صحيح أكبر من صفر.");
                return;
            }

            var invoice = new FuelInvoice
            {
                CarNumber = InvoiceCarComboBox.SelectedItem.ToString(),
                InvoiceNumber = (InvoiceNumberTextBox.Text ?? string.Empty).Trim(),
                Date = date,
                Amount = amount,
                IsPaid = IsPaidCheckBox.IsChecked ?? false,
                Year = FiscalYearHelper.GetSelectedYear(YearComboBox)
            };

            if (_invoiceToEdit == null)
            {
                SaveFuelInvoice(invoice);
                MessageBox.Show("تمت الإضافة بنجاح.");
            }
            else
            {
                invoice.Id = _invoiceToEdit.Id;
                UpdateFuelInvoice(invoice);
                MessageBox.Show("تم التحديث بنجاح.");
            }

            ClearFields_Click(null, null);
            RefreshFuelInvoicesGrid();
        }

        private void EditInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as Button)?.DataContext is FuelInvoice selected)) return;

            _invoiceToEdit = selected;
            InvoiceCarComboBox.SelectedItem = selected.CarNumber;
            InvoiceNumberTextBox.Text = selected.InvoiceNumber;
            InvoiceDateTextBox.Text = selected.Date.ToString(AppSettings.DateFormat);
            AmountTextBox.Text = selected.Amount.ToString("0.###");
            IsPaidCheckBox.IsChecked = selected.IsPaid;
            AddUpdateButton.Content = "تحديث الفاتورة";
        }

        private void DeleteInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as Button)?.DataContext is FuelInvoice selected)) return;

            if (MessageBox.Show(
                "هل أنت متأكد من حذف هذه الفاتورة؟ سيؤثر الحذف على الرصيد غير المدفوع والتقارير.",
                "تأكيد الحذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                DeleteFuelInvoice(selected.Id);
                RefreshFuelInvoicesGrid();
            }
        }

        private void ClearFields_Click(object sender, RoutedEventArgs e)
        {
            _invoiceToEdit = null;
            InvoiceCarComboBox.SelectedIndex = -1;
            InvoiceNumberTextBox.Clear();
            InvoiceDateTextBox.Clear();
            AmountTextBox.Clear();
            IsPaidCheckBox.IsChecked = false;
            AddUpdateButton.Content = "إضافة فاتورة";
        }

        private void CalculateTotal_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseDate(SummaryFromDateTextBox.Text, out DateTime fromDate, false))
            {
                MessageBox.Show("تاريخ البداية في ملخص الاستهلاك غير صحيح.", "خطأ في التاريخ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParseDate(SummaryToDateTextBox.Text, out DateTime toDate, false))
            {
                MessageBox.Show("تاريخ النهاية في ملخص الاستهلاك غير صحيح.", "خطأ في التاريخ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!FiscalYearHelper.ValidateDateInSelectedYear(fromDate, YearComboBox, "تاريخ بداية الملخص") ||
                !FiscalYearHelper.ValidateDateInSelectedYear(toDate, YearComboBox, "تاريخ نهاية الملخص"))
            {
                return;
            }

            if (fromDate.Date > toDate.Date)
            {
                MessageBox.Show(
                    "تاريخ بداية الملخص يجب أن يكون قبل أو مساوياً لتاريخ النهاية.",
                    "نطاق تاريخ غير صحيح",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string car = SummaryCarComboBox.SelectedItem as string;
            if (car == "الكل") car = null;

            decimal total = GetTotalForDateRange(car, fromDate, toDate);
            TotalAmountTextBlock.Text = total.ToString("N3");
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            var invoices = FuelInvoicesDataGrid.ItemsSource as List<FuelInvoice>;
            if (invoices == null || !invoices.Any())
            {
                MessageBox.Show("لا توجد فواتير للطباعة.");
                return;
            }

            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                FlowDocument doc = CreateFuelPrintDocument(invoices);
                doc.PagePadding = new Thickness(50);
                doc.ColumnWidth = printDialog.PrintableAreaWidth;
                printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "تقرير فواتير الوقود");
            }
        }
        #endregion

        #region Database Methods
        private List<Car> LoadCars()
        {
            var cars = new List<Car>();
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                const string sql = "SELECT Id, CarNumber FROM Cars ORDER BY CarNumber";
                using (var cmd = new SqliteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cars.Add(new Car
                        {
                            Id = reader.GetInt32(0),
                            CarNumber = reader.GetString(1)
                        });
                    }
                }
            }
            return cars;
        }

        private bool SaveCar(Car car)
        {
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    const string sql = "INSERT INTO Cars (CarNumber) VALUES (@CarNumber)";
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@CarNumber", car.CarNumber);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                return false;
            }
        }

        private bool HasFuelInvoices(string carNumber)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqliteCommand("SELECT EXISTS(SELECT 1 FROM FuelInvoices WHERE CarNumber = @CarNumber LIMIT 1)", conn))
                {
                    cmd.Parameters.AddWithValue("@CarNumber", carNumber);
                    return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
                }
            }
        }

        private void DeleteCar(int id)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqliteCommand("DELETE FROM Cars WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private List<FuelInvoice> LoadFuelInvoices(string carFilter, DateTime? fromDate, DateTime? toDate, bool unpaidOnly)
        {
            var visibleInvoices = new List<FuelInvoice>();
            int year = FiscalYearHelper.GetSelectedYear(YearComboBox);

            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                const string sql = @"SELECT Id, CarNumber, InvoiceNumber, Date, Amount, IsPaid, Year
                                     FROM FuelInvoices
                                     WHERE Year = @Year
                                     ORDER BY CarNumber, date(Date), Id";

                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Year", year);
                    using (var reader = cmd.ExecuteReader())
                    {
                        string currentCar = null;
                        decimal runningUnpaid = 0;

                        while (reader.Read())
                        {
                            var invoice = new FuelInvoice
                            {
                                Id = reader.GetInt32(0),
                                CarNumber = reader.GetString(1),
                                InvoiceNumber = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Date = ParseStoredDate(reader.GetString(3)),
                                Amount = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetDouble(4)),
                                IsPaid = !reader.IsDBNull(5) && reader.GetInt32(5) == 1,
                                Year = reader.GetInt32(6)
                            };

                            if (!string.Equals(currentCar, invoice.CarNumber, StringComparison.Ordinal))
                            {
                                currentCar = invoice.CarNumber;
                                runningUnpaid = 0;
                            }

                            if (!invoice.IsPaid)
                            {
                                runningUnpaid += invoice.Amount;
                            }
                            invoice.AccumulatedBalance = runningUnpaid;

                            bool matchesCar = string.IsNullOrWhiteSpace(carFilter) ||
                                string.Equals(invoice.CarNumber, carFilter, StringComparison.CurrentCultureIgnoreCase);
                            bool matchesFrom = !fromDate.HasValue || invoice.Date.Date >= fromDate.Value.Date;
                            bool matchesTo = !toDate.HasValue || invoice.Date.Date <= toDate.Value.Date;
                            bool matchesPaymentState = !unpaidOnly || !invoice.IsPaid;

                            if (matchesCar && matchesFrom && matchesTo && matchesPaymentState)
                            {
                                visibleInvoices.Add(invoice);
                            }
                        }
                    }
                }
            }

            return visibleInvoices;
        }

        private void SaveFuelInvoice(FuelInvoice invoice)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                const string sql = "INSERT INTO FuelInvoices (CarNumber, InvoiceNumber, Date, Amount, IsPaid, Year) VALUES (@CarNumber, @InvoiceNumber, @Date, @Amount, @IsPaid, @Year)";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@CarNumber", invoice.CarNumber);
                    cmd.Parameters.AddWithValue("@InvoiceNumber", (object)invoice.InvoiceNumber ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Date", invoice.Date.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@Amount", invoice.Amount);
                    cmd.Parameters.AddWithValue("@IsPaid", invoice.IsPaid ? 1 : 0);
                    cmd.Parameters.AddWithValue("@Year", invoice.Year);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void UpdateFuelInvoice(FuelInvoice invoice)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                const string sql = "UPDATE FuelInvoices SET CarNumber = @CarNumber, InvoiceNumber = @InvoiceNumber, Date = @Date, Amount = @Amount, IsPaid = @IsPaid, Year = @Year WHERE Id = @Id";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", invoice.Id);
                    cmd.Parameters.AddWithValue("@CarNumber", invoice.CarNumber);
                    cmd.Parameters.AddWithValue("@InvoiceNumber", (object)invoice.InvoiceNumber ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Date", invoice.Date.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@Amount", invoice.Amount);
                    cmd.Parameters.AddWithValue("@IsPaid", invoice.IsPaid ? 1 : 0);
                    cmd.Parameters.AddWithValue("@Year", invoice.Year);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void DeleteFuelInvoice(int id)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqliteCommand("DELETE FROM FuelInvoices WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private decimal GetTotalForDateRange(string carNumber, DateTime fromDate, DateTime toDate)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var sql = new StringBuilder("SELECT COALESCE(SUM(Amount), 0) FROM FuelInvoices WHERE Year = @Year AND date(Date) BETWEEN date(@FromDate) AND date(@ToDate)");
                using (var cmd = new SqliteCommand())
                {
                    cmd.Parameters.AddWithValue("@Year", FiscalYearHelper.GetSelectedYear(YearComboBox));
                    cmd.Parameters.AddWithValue("@FromDate", fromDate.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@ToDate", toDate.ToString("yyyy-MM-dd"));

                    if (!string.IsNullOrEmpty(carNumber))
                    {
                        sql.Append(" AND CarNumber = @CarNumber");
                        cmd.Parameters.AddWithValue("@CarNumber", carNumber);
                    }

                    cmd.Connection = conn;
                    cmd.CommandText = sql.ToString();
                    var result = cmd.ExecuteScalar();
                    return result == null || result == DBNull.Value ? 0 : Convert.ToDecimal(result);
                }
            }
        }

        private static DateTime ParseStoredDate(string value)
        {
            if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
            {
                return date;
            }

            return DateTime.Parse(value, CultureInfo.CurrentCulture);
        }
        #endregion

        #region Print Helper & Others
        private bool TryParseDate(string dateText, out DateTime date, bool showMessage = true)
        {
            int? defaultYear = null;
            if (YearComboBox.SelectedItem != null && int.TryParse(YearComboBox.SelectedItem.ToString(), out int year))
            {
                defaultYear = year;
            }

            return DatabaseService.TryParseDate(dateText, out date, showMessage, defaultYear);
        }

        private bool TryParseOptionalDate(string text, string fieldName, out DateTime? date)
        {
            date = null;
            if (string.IsNullOrWhiteSpace(text)) return true;

            if (!TryParseDate(text, out DateTime parsed, false))
            {
                MessageBox.Show($"{fieldName} غير صحيح.", "خطأ في التاريخ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!FiscalYearHelper.ValidateDateInSelectedYear(parsed, YearComboBox, fieldName))
            {
                return false;
            }

            date = parsed;
            return true;
        }

        private void ResetDateFieldsToSelectedYear()
        {
            FiscalYearHelper.ResetDateRange(YearComboBox, SearchFromDateTextBox, SearchToDateTextBox);
            FiscalYearHelper.ResetDateRange(YearComboBox, SummaryFromDateTextBox, SummaryToDateTextBox);
        }

        private void DateInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            TextBox txt = sender as TextBox;
            if (txt != null && !string.IsNullOrWhiteSpace(txt.Text))
            {
                if (!TryParseDate(txt.Text, out DateTime parsedDate, false)) return;
                txt.Text = parsedDate.ToString(AppSettings.DateFormat);
            }

            MoveFocusOnEnter(sender, e);
        }

        private FlowDocument CreateFuelPrintDocument(List<FuelInvoice> invoices)
        {
            var doc = new FlowDocument
            {
                FlowDirection = FlowDirection.RightToLeft,
                FontFamily = new FontFamily("Arial")
            };

            doc.Blocks.Add(new Paragraph(new Run($"تقرير فواتير الوقود للسنة المالية {YearComboBox.SelectedItem}"))
            {
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            });

            var table = new Table { CellSpacing = 0 };
            doc.Blocks.Add(table);
            for (int i = 0; i < 6; i++) table.Columns.Add(new TableColumn());

            var headerGroup = new TableRowGroup();
            table.RowGroups.Add(headerGroup);
            var headerRow = new TableRow { Background = Brushes.LightGray, FontWeight = FontWeights.Bold };
            headerGroup.Rows.Add(headerRow);
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("رقم السيارة"))) { Padding = new Thickness(5) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("رقم الفاتورة"))) { Padding = new Thickness(5) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("التاريخ"))) { Padding = new Thickness(5) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("المبلغ"))) { Padding = new Thickness(5) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("تم الدفع"))) { Padding = new Thickness(5) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("الرصيد غير المدفوع المتراكم"))) { Padding = new Thickness(5) });

            var dataGroup = new TableRowGroup();
            table.RowGroups.Add(dataGroup);
            foreach (var item in invoices)
            {
                var dataRow = new TableRow();
                dataGroup.Rows.Add(dataRow);
                dataRow.Cells.Add(new TableCell(new Paragraph(new Run(item.CarNumber))) { Padding = new Thickness(5), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 0, 1) });
                dataRow.Cells.Add(new TableCell(new Paragraph(new Run(item.InvoiceNumber))) { Padding = new Thickness(5), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 0, 1) });
                dataRow.Cells.Add(new TableCell(new Paragraph(new Run(item.Date.ToString(AppSettings.DateFormat)))) { Padding = new Thickness(5), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 0, 1) });
                dataRow.Cells.Add(new TableCell(new Paragraph(new Run(item.Amount.ToString("N3")))) { Padding = new Thickness(5), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 0, 1) });
                dataRow.Cells.Add(new TableCell(new Paragraph(new Run(item.IsPaid ? "نعم" : "لا"))) { Padding = new Thickness(5), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 0, 1) });
                dataRow.Cells.Add(new TableCell(new Paragraph(new Run(item.AccumulatedBalance.ToString("N3")))) { Padding = new Thickness(5), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 0, 1) });
            }

            return doc;
        }

        private void InvoiceDate_KeyDown(object sender, KeyEventArgs e)
        {
            DateInput_KeyDown(sender, e);
        }

        public void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var request = new TraversalRequest(FocusNavigationDirection.Next);
                if (Keyboard.FocusedElement is UIElement elementWithFocus)
                {
                    elementWithFocus.MoveFocus(request);
                }
                e.Handled = true;
            }
        }
        #endregion
    }
}
