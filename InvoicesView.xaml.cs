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
    public partial class InvoicesView : UserControl
    {
        private readonly string _connectionString = DatabaseService.ConnectionString;
        private Invoice _invoiceToEdit = null;

        public InvoicesView()
        {
            InitializeComponent();
            DatabaseService.InitializeDatabase();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            FiscalYearHelper.SelectCurrentYear(YearComboBox);
            ResetDateFieldsToSelectedYear();
            LoadOpeningBalance();
            RefreshInvoicesGrid();
        }

        private void YearComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                ResetDateFieldsToSelectedYear();
                LoadOpeningBalance();
                RefreshInvoicesGrid();
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            RefreshInvoicesGrid();
        }

        private void ShowAll_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Clear();
            ResetDateFieldsToSelectedYear();
            RefreshInvoicesGrid();
        }

        private void AddUpdateInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseDate(InvoiceDateTextBox.Text, out DateTime date, true)) return;
            if (!FiscalYearHelper.ValidateDateInSelectedYear(date, YearComboBox, "تاريخ الفاتورة")) return;

            var invoice = new Invoice
            {
                InvoiceNo = InvoiceNoTextBox.Text,
                Date = date,
                Description = DescriptionTextBox.Text,
                Debit = decimal.TryParse(DebitTextBox.Text, out var debit) ? debit : 0,
                Credit = decimal.TryParse(CreditTextBox.Text, out var credit) ? credit : 0,
                Year = FiscalYearHelper.GetSelectedYear(YearComboBox)
            };

            if (_invoiceToEdit == null)
            {
                SaveInvoice(invoice);
            }
            else
            {
                invoice.Id = _invoiceToEdit.Id;
                UpdateInvoice(invoice);
            }

            RefreshInvoicesGrid();
            ClearInputFields();
        }

        private void EditInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (InvoicesDataGrid.SelectedItem is Invoice selected)
            {
                _invoiceToEdit = selected;
                InvoiceNoTextBox.Text = selected.InvoiceNo;
                InvoiceDateTextBox.Text = selected.Date.ToString(AppSettings.DateFormat);
                DescriptionTextBox.Text = selected.Description;
                DebitTextBox.Text = selected.Debit > 0 ? selected.Debit.ToString() : "";
                CreditTextBox.Text = selected.Credit > 0 ? selected.Credit.ToString() : "";

                AddUpdateButton.Content = "تحديث الفاتورة";
                CancelEditButton.Visibility = Visibility.Visible;
            }
        }

        private void DeleteInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (InvoicesDataGrid.SelectedItem is Invoice selected)
            {
                if (MessageBox.Show("هل أنت متأكد من حذف هذه الفاتورة؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    using (var conn = new SqliteConnection(_connectionString))
                    {
                        conn.Open();
                        var cmd = new SqliteCommand("DELETE FROM Invoices WHERE Id = @Id", conn);
                        cmd.Parameters.AddWithValue("@Id", selected.Id);
                        cmd.ExecuteNonQuery();
                    }
                    RefreshInvoicesGrid();
                }
            }
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            ClearInputFields();
        }

        private void UpdateOpeningBalance_Click(object sender, RoutedEventArgs e)
        {
            if (YearComboBox.SelectedItem == null || !decimal.TryParse(OpeningBalanceTextBox.Text, out decimal balance))
            {
                MessageBox.Show("الرجاء إدخال رصيد افتتاحي صحيح.");
                return;
            }
            int year = int.Parse(YearComboBox.SelectedItem.ToString());

            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqliteCommand("INSERT OR REPLACE INTO OpeningBalances (Year, Balance) VALUES (@Year, @Balance)", conn);
                cmd.Parameters.AddWithValue("@Year", year);
                cmd.Parameters.AddWithValue("@Balance", balance);
                cmd.ExecuteNonQuery();
            }
            RefreshInvoicesGrid();
        }

        private void LoadOpeningBalance()
        {
            if (YearComboBox.SelectedItem == null) return;
            int year = int.Parse(YearComboBox.SelectedItem.ToString());

            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqliteCommand("SELECT Balance FROM OpeningBalances WHERE Year = @Year", conn);
                cmd.Parameters.AddWithValue("@Year", year);
                var result = cmd.ExecuteScalar();
                OpeningBalanceTextBox.Text = result != null ? Convert.ToDecimal(result).ToString() : "0";
            }
        }

        private void RefreshInvoicesGrid()
        {
            if (YearComboBox.SelectedItem == null) return;

            TryParseDate(FromDateTextBox.Text, out DateTime fromDate, false);
            TryParseDate(ToDateTextBox.Text, out DateTime toDate, false);

            var invoices = LoadInvoices(SearchTextBox.Text, fromDate == default ? (DateTime?)null : fromDate, toDate == default ? (DateTime?)null : toDate);

            // إعادة تعيين ItemsSource لضمان التحديث الصحيح
            InvoicesDataGrid.ItemsSource = null;
            InvoicesDataGrid.ItemsSource = invoices;

            decimal finalBalance = invoices.LastOrDefault()?.Balance ?? (decimal.TryParse(OpeningBalanceTextBox.Text, out var openBalance) ? openBalance : 0);
            InvoicesBalanceText.Text = finalBalance.ToString("N3");
        }

        private List<Invoice> LoadInvoices(string descriptionFilter, DateTime? fromDate, DateTime? toDate)
        {
            var list = new List<Invoice>();
            int year = int.Parse(YearComboBox.SelectedItem.ToString());
            decimal.TryParse(OpeningBalanceTextBox.Text, out decimal runningBalance);

            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var sql = new StringBuilder("SELECT Id, InvoiceNo, Date, Description, Debit, Credit FROM Invoices WHERE Year = @Year");
                var parameters = new Dictionary<string, object> { { "@Year", year } };
                if (!string.IsNullOrWhiteSpace(descriptionFilter)) { sql.Append(" AND Description LIKE @Desc"); parameters.Add("@Desc", "%" + descriptionFilter + "%"); }
                if (fromDate.HasValue) { sql.Append(" AND date(Date) >= date(@FromDate)"); parameters.Add("@FromDate", fromDate.Value.ToString("yyyy-MM-dd")); }
                if (toDate.HasValue) { sql.Append(" AND date(Date) <= date(@ToDate)"); parameters.Add("@ToDate", toDate.Value.ToString("yyyy-MM-dd")); }
                sql.Append(" ORDER BY date(Date), Id");

                using (var cmd = new SqliteCommand(sql.ToString(), conn))
                {
                    foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Key, p.Value);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var invoice = new Invoice
                            {
                                Id = reader.GetInt32(0),
                                InvoiceNo = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                Date = DateTime.TryParse(reader.GetString(2), out var parsedDate) ? parsedDate : DateTime.MinValue,
                                Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Debit = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetDouble(4)),
                                Credit = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetDouble(5)),
                            };
                            runningBalance += invoice.Debit - invoice.Credit;
                            invoice.Balance = runningBalance;
                            list.Add(invoice);
                        }
                    }
                }
            }
            return list;
        }

        private void SaveInvoice(Invoice invoice)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqliteCommand("INSERT INTO Invoices (InvoiceNo, Date, Description, Debit, Credit, Year) VALUES (@InvoiceNo, @Date, @Desc, @Debit, @Credit, @Year)", conn);
                cmd.Parameters.AddWithValue("@InvoiceNo", (object)invoice.InvoiceNo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Date", invoice.Date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@Desc", (object)invoice.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Debit", invoice.Debit);
                cmd.Parameters.AddWithValue("@Credit", invoice.Credit);
                cmd.Parameters.AddWithValue("@Year", invoice.Year);
                cmd.ExecuteNonQuery();
            }
        }

        private void UpdateInvoice(Invoice invoice)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var cmd = new SqliteCommand("UPDATE Invoices SET InvoiceNo=@InvoiceNo, Date=@Date, Description=@Desc, Debit=@Debit, Credit=@Credit, Year=@Year WHERE Id=@Id", conn);
                cmd.Parameters.AddWithValue("@Id", invoice.Id);
                cmd.Parameters.AddWithValue("@InvoiceNo", (object)invoice.InvoiceNo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Date", invoice.Date.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@Desc", (object)invoice.Description ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Debit", invoice.Debit);
                cmd.Parameters.AddWithValue("@Credit", invoice.Credit);
                cmd.Parameters.AddWithValue("@Year", invoice.Year);
                cmd.ExecuteNonQuery();
            }
        }

        private void ClearInputFields()
        {
            _invoiceToEdit = null;
            InvoiceNoTextBox.Clear();
            InvoiceDateTextBox.Clear();
            DescriptionTextBox.Clear();
            DebitTextBox.Clear();
            CreditTextBox.Clear();
            AddUpdateButton.Content = "إضافة الفاتورة";
            CancelEditButton.Visibility = Visibility.Collapsed;

            // --- هذا هو السطر الجديد الذي يعيد المؤشر إلى التاريخ ---
            InvoiceDateTextBox.Focus();
        }

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

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                FlowDocument doc = new FlowDocument();
                doc.FlowDirection = FlowDirection.RightToLeft;
                doc.FontFamily = new FontFamily("Arial");
                doc.PagePadding = new Thickness(60);
                doc.ColumnWidth = printDialog.PrintableAreaWidth;

                Paragraph header = new Paragraph();
                header.Inlines.Add(new Run("جمعية المركز الإسلامي الخيرية"));
                header.Inlines.Add(new LineBreak());
                header.Inlines.Add(new Run("مركز الرمثا للخدمات المجتمعية"));
                header.FontSize = 16;
                header.FontWeight = FontWeights.Bold;

                // يمكنك تعديل الاتجاه هنا إذا أردت (Left or Right)
                header.TextAlignment = TextAlignment.Left;

                header.Margin = new Thickness(0, 0, 0, 10);
                doc.Blocks.Add(header);

                Paragraph title = new Paragraph(new Run("تقرير الفواتير"));
                title.FontSize = 22;
                title.FontWeight = FontWeights.Bold;
                title.TextAlignment = TextAlignment.Center;
                title.Margin = new Thickness(0, 0, 0, 15);
                doc.Blocks.Add(title);

                Paragraph subHeader = new Paragraph(new Run($"تاريخ الطباعة: {DateTime.Now.ToString("dd/MM/yyyy")}"));
                subHeader.FontSize = 12;
                subHeader.TextAlignment = TextAlignment.Left;
                doc.Blocks.Add(subHeader);

                Table table = new Table();
                table.CellSpacing = 0;
                table.BorderThickness = new Thickness(1);
                table.BorderBrush = Brushes.Black;

                table.Columns.Add(new TableColumn { Width = new GridLength(14, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(10, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(42, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(11, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(11, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(12, GridUnitType.Star) });

                TableRowGroup headerGroup = new TableRowGroup();
                TableRow headerRow = new TableRow();
                headerRow.Background = Brushes.LightGray;

                string[] headers = { "التاريخ", "رقم الفاتورة", "البيان", "مدين", "دائن", "الرصيد" };
                foreach (string headerText in headers)
                {
                    headerRow.Cells.Add(CreateCell(headerText, true));
                }
                headerGroup.Rows.Add(headerRow);
                table.RowGroups.Add(headerGroup);

                TableRowGroup dataGroup = new TableRowGroup();
                if (InvoicesDataGrid.ItemsSource is IEnumerable<Invoice> invoices)
                {
                    foreach (var inv in invoices)
                    {
                        TableRow row = new TableRow();
                        row.Cells.Add(CreateCell(inv.Date.ToString(AppSettings.DateFormat)));
                        row.Cells.Add(CreateCell(inv.InvoiceNo));

                        var descCell = CreateCell(inv.Description);
                        descCell.TextAlignment = TextAlignment.Right;
                        row.Cells.Add(descCell);

                        row.Cells.Add(CreateCell(inv.Debit > 0 ? inv.Debit.ToString() : ""));
                        row.Cells.Add(CreateCell(inv.Credit > 0 ? inv.Credit.ToString() : ""));
                        row.Cells.Add(CreateCell(inv.Balance.ToString("N3")));
                        dataGroup.Rows.Add(row);
                    }
                }
                table.RowGroups.Add(dataGroup);
                doc.Blocks.Add(table);

                IDocumentPaginatorSource idpSource = doc;
                printDialog.PrintDocument(idpSource.DocumentPaginator, "تقرير الفواتير");
            }
        }

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

        private TableCell CreateCell(string text, bool isHeader = false)
        {
            Paragraph p = new Paragraph(new Run(text));
            p.Margin = new Thickness(4);
            p.TextAlignment = isHeader ? TextAlignment.Center : TextAlignment.Center;
            if (isHeader) p.FontWeight = FontWeights.Bold;

            TableCell cell = new TableCell(p);
            cell.BorderThickness = new Thickness(1);
            cell.BorderBrush = Brushes.Black;
            return cell;
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

        private void CreditTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddUpdateInvoice_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void OpeningBalanceTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                UpdateOpeningBalance_Click(sender, e);
            }
        }
        private void ResetDateFieldsToSelectedYear()
        {
            FiscalYearHelper.ResetDateRange(YearComboBox, FromDateTextBox, ToDateTextBox);
        }


    }
}
