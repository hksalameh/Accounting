using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
            if (!FiscalYearHelper.ValidateDateInSelectedYear(date, YearComboBox, "تاريخ الحركة")) return;
            if (!TryGetTransactionAmounts(out decimal fundAddition, out decimal invoiceExpense)) return;

            var invoice = new Invoice
            {
                InvoiceNo = InvoiceNoTextBox.Text?.Trim(),
                Date = date,
                Description = DescriptionTextBox.Text?.Trim(),
                // نحافظ على أسماء أعمدة قاعدة البيانات القديمة للتوافق:
                // Debit = تغذية الصندوق، Credit = صرف فاتورة.
                Debit = fundAddition,
                Credit = invoiceExpense,
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

        private bool TryGetTransactionAmounts(out decimal fundAddition, out decimal invoiceExpense)
        {
            fundAddition = 0;
            invoiceExpense = 0;

            bool hasFundAddition = !string.IsNullOrWhiteSpace(DebitTextBox.Text);
            bool hasInvoiceExpense = !string.IsNullOrWhiteSpace(CreditTextBox.Text);

            if (!hasFundAddition && !hasInvoiceExpense)
            {
                MessageBox.Show(
                    "أدخل مبلغاً في تغذية الصندوق أو في صرف الفاتورة.",
                    "المبلغ مطلوب",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (hasFundAddition && hasInvoiceExpense)
            {
                MessageBox.Show(
                    "الحركة الواحدة لا يمكن أن تكون تغذية للصندوق وصرف فاتورة في نفس الوقت.\n\nأدخل المبلغ في حقل واحد فقط.",
                    "نوع الحركة غير واضح",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (hasFundAddition && (!decimal.TryParse(DebitTextBox.Text, out fundAddition) || fundAddition <= 0))
            {
                MessageBox.Show("الرجاء إدخال مبلغ صحيح أكبر من صفر في تغذية الصندوق.");
                return false;
            }

            if (hasInvoiceExpense && (!decimal.TryParse(CreditTextBox.Text, out invoiceExpense) || invoiceExpense <= 0))
            {
                MessageBox.Show("الرجاء إدخال مبلغ صحيح أكبر من صفر في صرف الفاتورة.");
                return false;
            }

            return true;
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

                AddUpdateButton.Content = "تحديث الحركة";
                CancelEditButton.Visibility = Visibility.Visible;
            }
        }

        private void DeleteInvoice_Click(object sender, RoutedEventArgs e)
        {
            if (InvoicesDataGrid.SelectedItem is Invoice selected)
            {
                if (MessageBox.Show(
                    "هل أنت متأكد من حذف هذه الحركة؟",
                    "تأكيد الحذف",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    using (var conn = new SqliteConnection(_connectionString))
                    {
                        conn.Open();
                        using (var cmd = new SqliteCommand("DELETE FROM Invoices WHERE Id = @Id", conn))
                        {
                            cmd.Parameters.AddWithValue("@Id", selected.Id);
                            cmd.ExecuteNonQuery();
                        }
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
            if (YearComboBox.SelectedItem == null ||
                !decimal.TryParse(OpeningBalanceTextBox.Text, out decimal balance) ||
                balance < 0)
            {
                MessageBox.Show("الرجاء إدخال رصيد افتتاحي صحيح (صفر أو أكبر).");
                return;
            }

            int year = int.Parse(YearComboBox.SelectedItem.ToString());

            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqliteCommand("INSERT OR REPLACE INTO OpeningBalances (Year, Balance) VALUES (@Year, @Balance)", conn))
                {
                    cmd.Parameters.AddWithValue("@Year", year);
                    cmd.Parameters.AddWithValue("@Balance", balance);
                    cmd.ExecuteNonQuery();
                }
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
                using (var cmd = new SqliteCommand("SELECT Balance FROM OpeningBalances WHERE Year = @Year", conn))
                {
                    cmd.Parameters.AddWithValue("@Year", year);
                    var result = cmd.ExecuteScalar();
                    OpeningBalanceTextBox.Text = result != null && result != DBNull.Value
                        ? Convert.ToDecimal(result).ToString()
                        : "0";
                }
            }
        }

        private void RefreshInvoicesGrid()
        {
            if (YearComboBox.SelectedItem == null) return;

            TryParseDate(FromDateTextBox.Text, out DateTime fromDate, false);
            TryParseDate(ToDateTextBox.Text, out DateTime toDate, false);

            DateTime? from = fromDate == default(DateTime) ? (DateTime?)null : fromDate;
            DateTime? to = toDate == default(DateTime) ? (DateTime?)null : toDate;

            var invoices = LoadInvoices(SearchTextBox.Text, from, to);

            InvoicesDataGrid.ItemsSource = null;
            InvoicesDataGrid.ItemsSource = invoices;

            // رصيد الصندوق لا يتأثر بفلتر البيان؛ هو الرصيد الحقيقي حتى تاريخ نهاية البحث.
            InvoicesBalanceText.Text = GetBalanceAsOf(to).ToString("N3");
        }

        private List<Invoice> LoadInvoices(string descriptionFilter, DateTime? fromDate, DateTime? toDate)
        {
            var visibleItems = new List<Invoice>();
            int year = int.Parse(YearComboBox.SelectedItem.ToString());
            decimal.TryParse(OpeningBalanceTextBox.Text, out decimal runningBalance);
            string filter = descriptionFilter?.Trim();

            // نحمّل كل حركات السنة بالترتيب أولاً حتى نحسب الرصيد الحقيقي لكل سجل.
            // بعد ذلك فقط نطبّق فلاتر العرض. بهذه الطريقة لا تختفي الحركات السابقة من حساب الرصيد.
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                const string sql = @"SELECT Id, InvoiceNo, Date, Description, Debit, Credit
                                     FROM Invoices
                                     WHERE Year = @Year
                                     ORDER BY date(Date), Id";

                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Year", year);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string dateText = reader.GetString(2);
                            DateTime parsedDate;
                            if (!DateTime.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate) &&
                                !DateTime.TryParse(dateText, out parsedDate))
                            {
                                continue;
                            }

                            var invoice = new Invoice
                            {
                                Id = reader.GetInt32(0),
                                InvoiceNo = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                Date = parsedDate,
                                Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Debit = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetDouble(4)),
                                Credit = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetDouble(5)),
                                Year = year
                            };

                            runningBalance += invoice.Debit - invoice.Credit;
                            invoice.Balance = runningBalance;

                            bool matchesDescription = string.IsNullOrWhiteSpace(filter) ||
                                (invoice.Description ?? "").IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0;
                            bool matchesFrom = !fromDate.HasValue || invoice.Date.Date >= fromDate.Value.Date;
                            bool matchesTo = !toDate.HasValue || invoice.Date.Date <= toDate.Value.Date;

                            if (matchesDescription && matchesFrom && matchesTo)
                            {
                                visibleItems.Add(invoice);
                            }
                        }
                    }
                }
            }

            return visibleItems;
        }

        private decimal GetBalanceAsOf(DateTime? toDate)
        {
            int year = FiscalYearHelper.GetSelectedYear(YearComboBox);
            decimal.TryParse(OpeningBalanceTextBox.Text, out decimal openingBalance);
            decimal additions = 0;
            decimal expenses = 0;

            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                string sql = @"SELECT COALESCE(SUM(Debit), 0), COALESCE(SUM(Credit), 0)
                               FROM Invoices
                               WHERE Year = @Year";
                if (toDate.HasValue)
                {
                    sql += " AND date(Date) <= date(@ToDate)";
                }

                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Year", year);
                    if (toDate.HasValue)
                    {
                        cmd.Parameters.AddWithValue("@ToDate", toDate.Value.ToString("yyyy-MM-dd"));
                    }

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            additions = reader.IsDBNull(0) ? 0 : Convert.ToDecimal(reader.GetDouble(0));
                            expenses = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetDouble(1));
                        }
                    }
                }
            }

            return openingBalance + additions - expenses;
        }

        private void SaveInvoice(Invoice invoice)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqliteCommand("INSERT INTO Invoices (InvoiceNo, Date, Description, Debit, Credit, Year) VALUES (@InvoiceNo, @Date, @Desc, @Debit, @Credit, @Year)", conn))
                {
                    cmd.Parameters.AddWithValue("@InvoiceNo", (object)invoice.InvoiceNo ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Date", invoice.Date.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@Desc", (object)invoice.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Debit", invoice.Debit);
                    cmd.Parameters.AddWithValue("@Credit", invoice.Credit);
                    cmd.Parameters.AddWithValue("@Year", invoice.Year);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void UpdateInvoice(Invoice invoice)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqliteCommand("UPDATE Invoices SET InvoiceNo=@InvoiceNo, Date=@Date, Description=@Desc, Debit=@Debit, Credit=@Credit, Year=@Year WHERE Id=@Id", conn))
                {
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
        }

        private void ClearInputFields()
        {
            _invoiceToEdit = null;
            InvoiceNoTextBox.Clear();
            InvoiceDateTextBox.Clear();
            DescriptionTextBox.Clear();
            DebitTextBox.Clear();
            CreditTextBox.Clear();
            AddUpdateButton.Content = "إضافة حركة";
            CancelEditButton.Visibility = Visibility.Collapsed;
            InvoiceDateTextBox.Focus();
        }

        private bool TryParseDate(string dateText, out DateTime date, bool showMessage = true)
        {
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
                    if (TryParseDate(txt.Text, out DateTime parsedDate, false))
                    {
                        txt.Text = parsedDate.ToString(AppSettings.DateFormat);
                    }
                    else
                    {
                        return;
                    }

                    e.Handled = true;
                    TraversalRequest request = new TraversalRequest(FocusNavigationDirection.Next) { Wrapped = true };
                    ((UIElement)sender).MoveFocus(request);
                }
            }
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                FlowDocument doc = new FlowDocument
                {
                    FlowDirection = FlowDirection.RightToLeft,
                    FontFamily = new FontFamily("Arial"),
                    PagePadding = new Thickness(60),
                    ColumnWidth = printDialog.PrintableAreaWidth
                };

                Paragraph header = new Paragraph
                {
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                header.Inlines.Add(new Run("جمعية المركز الإسلامي الخيرية"));
                header.Inlines.Add(new LineBreak());
                header.Inlines.Add(new Run("مركز الرمثا للخدمات المجتمعية"));
                doc.Blocks.Add(header);

                doc.Blocks.Add(new Paragraph(new Run($"كشف حركة الصندوق - السنة المالية {YearComboBox.SelectedItem}"))
                {
                    FontSize = 22,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 15)
                });

                doc.Blocks.Add(new Paragraph(new Run($"تاريخ الطباعة: {DateTime.Now:dd/MM/yyyy}"))
                {
                    FontSize = 12,
                    TextAlignment = TextAlignment.Left
                });

                Table table = new Table
                {
                    CellSpacing = 0,
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brushes.Black
                };

                table.Columns.Add(new TableColumn { Width = new GridLength(14, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(12, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(38, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(12, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(12, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(12, GridUnitType.Star) });

                TableRowGroup headerGroup = new TableRowGroup();
                TableRow headerRow = new TableRow { Background = Brushes.LightGray };
                string[] headers = { "التاريخ", "المرجع", "البيان", "تغذية الصندوق", "صرف فاتورة", "الرصيد" };
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

                        row.Cells.Add(CreateCell(inv.Debit > 0 ? inv.Debit.ToString("N3") : ""));
                        row.Cells.Add(CreateCell(inv.Credit > 0 ? inv.Credit.ToString("N3") : ""));
                        row.Cells.Add(CreateCell(inv.Balance.ToString("N3")));
                        dataGroup.Rows.Add(row);
                    }
                }
                table.RowGroups.Add(dataGroup);
                doc.Blocks.Add(table);

                IDocumentPaginatorSource idpSource = doc;
                printDialog.PrintDocument(idpSource.DocumentPaginator, "كشف حركة الصندوق");
            }
        }

        private void InvoiceDate_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TextBox txt = sender as TextBox;
                if (txt != null && !string.IsNullOrWhiteSpace(txt.Text))
                {
                    if (TryParseDate(txt.Text, out DateTime parsedDate, false))
                    {
                        txt.Text = parsedDate.ToString(AppSettings.DateFormat);
                    }
                    else
                    {
                        MessageBox.Show("التاريخ غير صحيح.", "خطأ في التاريخ", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                MoveFocusOnEnter(sender, e);
            }
        }

        private TableCell CreateCell(string text, bool isHeader = false)
        {
            Paragraph p = new Paragraph(new Run(text ?? ""))
            {
                Margin = new Thickness(4),
                TextAlignment = TextAlignment.Center
            };
            if (isHeader) p.FontWeight = FontWeights.Bold;

            return new TableCell(p)
            {
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Black
            };
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
