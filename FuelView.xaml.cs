﻿using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Printing;
using System.Windows.Input;

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

            var currentSearch = SearchCarComboBox.SelectedItem;
            SearchCarComboBox.ItemsSource = searchCarList;
            SearchCarComboBox.SelectedItem = currentSearch ?? "الكل";

            var currentInvoiceCar = InvoiceCarComboBox.SelectedItem;
            InvoiceCarComboBox.ItemsSource = cars;
            InvoiceCarComboBox.SelectedItem = currentInvoiceCar;

            var currentSummaryCar = SummaryCarComboBox.SelectedItem;
            SummaryCarComboBox.ItemsSource = searchCarList;
            SummaryCarComboBox.SelectedItem = currentSummaryCar ?? "الكل";
        }

        private void RefreshFuelInvoicesGrid()
        {
            if (YearComboBox.SelectedItem == null) return;
            string selectedCar = SearchCarComboBox.SelectedItem as string;
            if (selectedCar == "الكل") selectedCar = null;

            DateTime? fromDate = null, toDate = null;
            if (TryParseDate(SearchFromDateTextBox.Text, out DateTime fromDt, false)) fromDate = fromDt;
            if (TryParseDate(SearchToDateTextBox.Text, out DateTime toDt, false)) toDate = toDt;

            bool unpaidOnly = ShowUnpaidOnlyCheckBox.IsChecked ?? false;
            var invoices = LoadFuelInvoices(selectedCar, fromDate, toDate, unpaidOnly);

            // إعادة تعيين ItemsSource لضمان التحديث الصحيح
            FuelInvoicesDataGrid.ItemsSource = null;
            FuelInvoicesDataGrid.ItemsSource = invoices;
        }

        private void RefreshCarsGrid()
        {
            CarsDataGrid.ItemsSource = LoadCars();
        }

        private void RefreshData_Event(object sender, RoutedEventArgs e)
        {
            if (sender == YearComboBox)
            {
                ResetDateFieldsToSelectedYear();
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
            string newCarNumber = NewCarNumberTextBox.Text.Trim();
            if (string.IsNullOrEmpty(newCarNumber)) { MessageBox.Show("الرجاء إدخال رقم السيارة."); return; }
            if (SaveCar(new Car { CarNumber = newCarNumber }))
            {
                MessageBox.Show("تمت إضافة السيارة بنجاح.");
                NewCarNumberTextBox.Clear();
                RefreshAllData();
            }
            else { MessageBox.Show("هذه السيارة موجودة بالفعل."); }
        }

        private void DeleteCar_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is Car selectedCar)
            {
                if (MessageBox.Show($"هل أنت متأكد من حذف السيارة {selectedCar.CarNumber}؟ سيتم حذف جميع فواتيرها.", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    DeleteCar(selectedCar.Id);
                    RefreshAllData();
                }
            }
        }
        #endregion

        #region Invoice Management
        private void AddUpdateInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (InvoiceCarComboBox.SelectedItem == null || !TryParseDate(InvoiceDateTextBox.Text, out DateTime date) || string.IsNullOrWhiteSpace(AmountTextBox.Text) || !decimal.TryParse(AmountTextBox.Text, out decimal amount) || amount < 0)
            {
                MessageBox.Show("الرجاء ملء الحقول المطلوبة (السيارة، تاريخ صحيح، ومبلغ صحيح).");
                return;
            }
            if (!FiscalYearHelper.ValidateDateInSelectedYear(date, YearComboBox, "تاريخ فاتورة الوقود")) return;

            var invoice = new FuelInvoice
            {
                CarNumber = InvoiceCarComboBox.SelectedItem.ToString(),
                InvoiceNumber = InvoiceNumberTextBox.Text,
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
            if ((sender as Button)?.DataContext is FuelInvoice selected)
            {
                _invoiceToEdit = selected;
                InvoiceCarComboBox.SelectedItem = selected.CarNumber;
                InvoiceNumberTextBox.Text = selected.InvoiceNumber;
                InvoiceDateTextBox.Text = selected.Date.ToString(AppSettings.DateFormat);
                AmountTextBox.Text = selected.Amount.ToString(CultureInfo.InvariantCulture);
                IsPaidCheckBox.IsChecked = selected.IsPaid;
                AddUpdateButton.Content = "تحديث الفاتورة";
            }
        }

        private void DeleteInvoice_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is FuelInvoice selected)
            {
                if (MessageBox.Show("هل أنت متأكد من حذف هذه الفاتورة؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    DeleteFuelInvoice(selected.Id);
                    RefreshFuelInvoicesGrid();
                }
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
            // نستخدم showMessage: false لمنع ظهور رسالتين متتاليتين ولتحديد الخطأ بدقة
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

            string car = SummaryCarComboBox.SelectedItem as string;
            if (car == "الكل") car = null;
            decimal total = GetTotalForDateRange(car, fromDate, toDate);
            TotalAmountTextBlock.Text = total.ToString("N3");
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            var invoices = FuelInvoicesDataGrid.ItemsSource as List<FuelInvoice>;
            if (invoices == null || !invoices.Any()) { MessageBox.Show("لا توجد فواتير للطباعة."); return; }
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
                var sql = "SELECT Id, CarNumber FROM Cars ORDER BY CarNumber";
                using (var cmd = new SqliteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cars.Add(new Car { Id = reader.GetInt32(0), CarNumber = reader.GetString(1) });
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
                    var sql = "INSERT INTO Cars (CarNumber) VALUES (@CarNumber)";
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

        private void DeleteCar(int id)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    var cmd1 = conn.CreateCommand();
                    cmd1.Transaction = transaction;
                    cmd1.CommandText = "DELETE FROM FuelInvoices WHERE CarNumber = (SELECT CarNumber FROM Cars WHERE Id = @Id)";
                    cmd1.Parameters.AddWithValue("@Id", id);
                    cmd1.ExecuteNonQuery();

                    var cmd2 = conn.CreateCommand();
                    cmd2.Transaction = transaction;
                    cmd2.CommandText = "DELETE FROM Cars WHERE Id = @Id";
                    cmd2.Parameters.AddWithValue("@Id", id);
                    cmd2.ExecuteNonQuery();

                    transaction.Commit();
                }
            }
        }

        private List<FuelInvoice> LoadFuelInvoices(string carFilter, DateTime? fromDate, DateTime? toDate, bool unpaidOnly)
        {
            var invoices = new List<FuelInvoice>();
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var sql = new StringBuilder("SELECT Id, CarNumber, InvoiceNumber, Date, Amount, IsPaid, Year FROM FuelInvoices WHERE Year = @Year");
                var parameters = new Dictionary<string, object> { { "@Year", FiscalYearHelper.GetSelectedYear(YearComboBox) } };
                if (!string.IsNullOrEmpty(carFilter)) { sql.Append(" AND CarNumber = @CarNumber"); parameters.Add("@CarNumber", carFilter); }
                if (fromDate.HasValue) { sql.Append(" AND date(Date) >= date(@FromDate)"); parameters.Add("@FromDate", fromDate.Value.ToString("yyyy-MM-dd")); }
                if (toDate.HasValue) { sql.Append(" AND date(Date) <= date(@ToDate)"); parameters.Add("@ToDate", toDate.Value.ToString("yyyy-MM-dd")); }
                if (unpaidOnly) { sql.Append(" AND IsPaid = 0"); }
                sql.Append(" ORDER BY CarNumber, Date");

                using (var cmd = new SqliteCommand(sql.ToString(), conn))
                {
                    foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Key, p.Value);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            invoices.Add(new FuelInvoice
                            {
                                Id = reader.GetInt32(0),
                                CarNumber = reader.GetString(1),
                                InvoiceNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Date = DateTime.Parse(reader.GetString(3)),
                                Amount = Convert.ToDecimal(reader.GetDouble(4)),
                                IsPaid = reader.GetInt32(5) == 1,
                                Year = reader.GetInt32(6)
                            });
                        }
                    }
                }
            }

            decimal runningTotal = 0;
            string currentCar = null;
            foreach (var invoice in invoices)
            {
                if (invoice.CarNumber != currentCar) { runningTotal = 0; currentCar = invoice.CarNumber; }
                if (!invoice.IsPaid) { runningTotal += invoice.Amount; }
                invoice.AccumulatedBalance = runningTotal;
            }
            return invoices;
        }

        private void SaveFuelInvoice(FuelInvoice invoice)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var sql = "INSERT INTO FuelInvoices (CarNumber, InvoiceNumber, Date, Amount, IsPaid, Year) VALUES (@CarNumber, @InvoiceNumber, @Date, @Amount, @IsPaid, @Year)";
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
                var sql = "UPDATE FuelInvoices SET CarNumber = @CarNumber, InvoiceNumber = @InvoiceNumber, Date = @Date, Amount = @Amount, IsPaid = @IsPaid, Year = @Year WHERE Id = @Id";
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
                var sql = "DELETE FROM FuelInvoices WHERE Id = @Id";
                using (var cmd = new SqliteCommand(sql, conn))
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
                var sql = new StringBuilder("SELECT SUM(Amount) FROM FuelInvoices WHERE Year = @Year AND date(Date) BETWEEN @FromDate AND @ToDate");
                var cmd = new SqliteCommand();
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
                if (result != DBNull.Value && result != null)
                {
                    return Convert.ToDecimal(result);
                }
            }
            return 0;
        }
        #endregion

        #region Print Helper & Others
        private bool TryParseDate(string dateText, out DateTime date, bool showMessage = true)
        {
            // استخراج السنة المحددة من ComboBox
            int? defaultYear = null;
            if (YearComboBox.SelectedItem != null && int.TryParse(YearComboBox.SelectedItem.ToString(), out int year))
            {
                defaultYear = year;
            }
            return DatabaseService.TryParseDate(dateText, out date, showMessage, defaultYear);
        }

        private void ResetDateFieldsToSelectedYear()
        {
            FiscalYearHelper.ResetDateRange(YearComboBox, SearchFromDateTextBox, SearchToDateTextBox);
            FiscalYearHelper.ResetDateRange(YearComboBox, SummaryFromDateTextBox, SummaryToDateTextBox);
        }

        private void DateInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TextBox txt = sender as TextBox;
                if (txt != null && !string.IsNullOrWhiteSpace(txt.Text))
                {
                    // استخدام TryParseDate المحسّنة مع السنة من ComboBox
                    if (TryParseDate(txt.Text, out DateTime parsedDate, false))
                    {
                        txt.Text = parsedDate.ToString(AppSettings.DateFormat);
                    }
                    else
                    {
                        return; // لا تنقل التركيز إذا كان التاريخ غير صحيح
                    }

                    // نقل التركيز للحقل التالي
                    e.Handled = true;
                    TraversalRequest request = new TraversalRequest(FocusNavigationDirection.Next);
                    request.Wrapped = true;
                    ((UIElement)sender).MoveFocus(request);
                }
            }
        }

        private FlowDocument CreateFuelPrintDocument(List<FuelInvoice> invoices)
        {
            var doc = new FlowDocument { FlowDirection = FlowDirection.RightToLeft, FontFamily = new FontFamily("Arial") };
            doc.Blocks.Add(new Paragraph(new Run($"تقرير فواتير الوقود للسنة المالية {YearComboBox.SelectedItem}")) { FontSize = 20, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 20) });

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
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("الرصيد المتراكم"))) { Padding = new Thickness(5) });

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

        // --- هذا هو الجزء الجديد لإكمال التاريخ ---
        private void InvoiceDate_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TextBox txt = sender as TextBox;
                if (txt != null && !string.IsNullOrWhiteSpace(txt.Text))
                {
                    // محاولة إكمال التاريخ إذا كان بصيغة يوم/شهر فقط
                    string[] parts = txt.Text.Split('/', '-');
                    if (parts.Length == 2)
                    {
                        if (int.TryParse(parts[0], out int day) && int.TryParse(parts[1], out int month))
                        {
                            try
                            {
                                int year = int.Parse(YearComboBox.SelectedItem.ToString());
                                // التحقق من صحة التاريخ
                                int daysInMonth = DateTime.DaysInMonth(year, month);
                                if (day >= 1 && day <= daysInMonth && month >= 1 && month <= 12)
                                {
                                    DateTime date = new DateTime(year, month, day);
                                    txt.Text = date.ToString(AppSettings.DateFormat);
                                }
                                else
                                {
                                    MessageBox.Show($"التاريخ غير صحيح: شهر {month} لا يحتوي على {day} يوم.\nعدد أيام هذا الشهر هو {daysInMonth} يوم.", "خطأ في التاريخ", MessageBoxButton.OK, MessageBoxImage.Warning);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("التاريخ غير صحيح: " + ex.Message, "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
                MoveFocusOnEnter(sender, e);
            }
        }
        // ----------------------------------------

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
