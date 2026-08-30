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
    public partial class RevenuesView : UserControl
    {
        private readonly string _connectionString = DatabaseService.ConnectionString;
        private ReceiptVoucher _receiptToEdit = null;
        private Deposit _depositToEdit = null;

        public RevenuesView()
        {
            InitializeComponent();
            DatabaseService.InitializeDatabase();
        }

        #region Event Handlers
        private void RevenuesView_Loaded(object sender, RoutedEventArgs e)
        {
            FiscalYearHelper.SelectCurrentYear(YearComboBox);

            // استدعاء الدالة الجديدة لضبط تواريخ البحث بناءً على السنة المختارة
            ResetDateFieldsToSelectedYear();

            RefreshGrids();
        }


        private void AddUpdateReceiptVoucher_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseDate(ReceiptDateTextBox.Text, out DateTime date)) { return; }
            if (!FiscalYearHelper.ValidateDateInSelectedYear(date, YearComboBox, "تاريخ سند القبض")) return;
            if (!decimal.TryParse(ReceiptAmountTextBox.Text, out decimal amount)) { MessageBox.Show("الرجاء إدخال مبلغ صحيح."); return; }

            var voucher = new ReceiptVoucher
            {
                Date = date,
                BookNo = ReceiptBookNoTextBox.Text,
                FromVoucherNo = int.TryParse(ReceiptFromVoucherNoTextBox.Text, out int from) ? from : 0,
                ToVoucherNo = int.TryParse(ReceiptToVoucherNoTextBox.Text, out int to) ? to : 0,
                Recipient = ReceiptRecipientTextBox.Text,
                Amount = amount,
                Year = FiscalYearHelper.GetSelectedYear(YearComboBox),
                Notes = ReceiptNotesTextBox.Text
            };

            if (_receiptToEdit == null) { SaveReceiptVoucher(voucher); } else { voucher.Id = _receiptToEdit.Id; UpdateReceiptVoucherInDb(voucher); }
            RefreshGrids();
            ClearReceiptVoucherFields();
            ExitEditMode();
        }

        private void AddUpdateDeposit_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseDate(DepositDateTextBox.Text, out DateTime date)) { return; }
            if (!FiscalYearHelper.ValidateDateInSelectedYear(date, YearComboBox, "تاريخ الإيداع")) return;
            if (!decimal.TryParse(DepositAmountTextBox.Text, out decimal amount)) { MessageBox.Show("الرجاء إدخال مبلغ صحيح."); return; }

            var deposit = new Deposit
            {
                Date = date,
                DepositorName = DepositorNameTextBox.Text,
                Amount = amount,
                Year = FiscalYearHelper.GetSelectedYear(YearComboBox),
                Notes = DepositNotesTextBox.Text
            };

            if (_depositToEdit == null) { SaveDeposit(deposit); } else { deposit.Id = _depositToEdit.Id; UpdateDepositInDb(deposit); }
            RefreshGrids();
            ClearDepositFields();
            ExitEditMode();
        }

        private void EditReceipt_Click(object sender, RoutedEventArgs e)
        {
            if (ReceiptsDataGrid.SelectedItem is ReceiptVoucher selected)
            {
                _receiptToEdit = selected;
                ReceiptDateTextBox.Text = selected.Date.ToString(AppSettings.DateFormat);
                ReceiptBookNoTextBox.Text = selected.BookNo;
                ReceiptFromVoucherNoTextBox.Text = selected.FromVoucherNo.ToString();
                ReceiptToVoucherNoTextBox.Text = selected.ToVoucherNo.ToString();
                ReceiptRecipientTextBox.Text = selected.Recipient;
                ReceiptAmountTextBox.Text = selected.Amount.ToString();
                ReceiptNotesTextBox.Text = selected.Notes;
                EnterEditMode(true);
            }
        }

        private void EditDeposit_Click(object sender, RoutedEventArgs e)
        {
            if (DepositsDataGrid.SelectedItem is Deposit selected)
            {
                _depositToEdit = selected;
                DepositDateTextBox.Text = selected.Date.ToString(AppSettings.DateFormat);
                DepositorNameTextBox.Text = selected.DepositorName;
                DepositAmountTextBox.Text = selected.Amount.ToString();
                DepositNotesTextBox.Text = selected.Notes;
                EnterEditMode(false);
            }
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e) { ExitEditMode(); }

        private void DeleteReceipt_Click(object sender, RoutedEventArgs e)
        {
            if (ReceiptsDataGrid.SelectedItem is ReceiptVoucher selected)
            {
                if (MessageBox.Show("هل أنت متأكد من حذف هذا السند؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    DeleteFromDb("ReceiptVouchers", selected.Id);
                    RefreshGrids();
                }
            }
        }

        private void DeleteDeposit_Click(object sender, RoutedEventArgs e)
        {
            if (DepositsDataGrid.SelectedItem is Deposit selected)
            {
                if (MessageBox.Show("هل أنت متأكد من حذف هذا الإيداع؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    DeleteFromDb("Deposits", selected.Id);
                    RefreshGrids();
                }
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e) { RefreshGrids(); }
        private void ShowAll_Click(object sender, RoutedEventArgs e)
        {
            SearchBookNoTextBox.Clear();
            SearchRecipientTextBox.Clear();

            // إعادة ضبط حقول التواريخ بناءً على السنة المالية المختارة بالـ ComboBox حالياً
            ResetDateFieldsToSelectedYear();
            RefreshGrids();
        }

        private void YearComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                // تحديث نطاق التواريخ ليتماشى مع السنة المالية الجديدة المختارة
                ResetDateFieldsToSelectedYear();
                RefreshGrids();
            }
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            var receipts = ReceiptsDataGrid.ItemsSource as List<ReceiptVoucher>;
            var deposits = DepositsDataGrid.ItemsSource as List<Deposit>;
            if ((receipts == null || !receipts.Any()) && (deposits == null || !deposits.Any()))
            {
                MessageBox.Show("لا توجد بيانات للطباعة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                FlowDocument doc = CreateRevenuesPrintDocument(receipts, deposits);
                doc.PagePadding = new Thickness(50);
                doc.ColumnWidth = printDialog.PrintableAreaWidth;
                printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "تقرير الإيرادات");
            }
        }
        private void ResetDateFieldsToSelectedYear()
        {
            FiscalYearHelper.ResetDateRange(YearComboBox, SearchFromDateTextBox, SearchToDateTextBox);
        }
        private void InputTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // عند تغيير التبويب العلوي، نقوم بتغيير التبويب السفلي تلقائياً ليطابقه
            if (InputTabControl != null && DataTabControl != null && e.Source == InputTabControl)
            {
                DataTabControl.SelectedIndex = InputTabControl.SelectedIndex;
            }
        }

        private void DataTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // عند تغيير التبويب السفلي، نقوم بتغيير التبويب العلوي تلقائياً ليطابقه
            if (InputTabControl != null && DataTabControl != null && e.Source == DataTabControl)
            {
                InputTabControl.SelectedIndex = DataTabControl.SelectedIndex;
            }
        }
        #endregion

        #region Database Interaction
        private void SaveReceiptVoucher(ReceiptVoucher receipt)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var sql = "INSERT INTO ReceiptVouchers (Date, BookNo, FromVoucherNo, ToVoucherNo, Recipient, Amount, Year, Notes) VALUES (@Date, @BookNo, @FromVoucherNo, @ToVoucherNo, @Recipient, @Amount, @Year, @Notes)";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Date", receipt.Date.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@BookNo", receipt.BookNo);
                    cmd.Parameters.AddWithValue("@FromVoucherNo", receipt.FromVoucherNo);
                    cmd.Parameters.AddWithValue("@ToVoucherNo", receipt.ToVoucherNo);
                    cmd.Parameters.AddWithValue("@Recipient", receipt.Recipient);
                    cmd.Parameters.AddWithValue("@Amount", receipt.Amount);
                    cmd.Parameters.AddWithValue("@Year", receipt.Year);
                    cmd.Parameters.AddWithValue("@Notes", (object)receipt.Notes ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void UpdateReceiptVoucherInDb(ReceiptVoucher receipt)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var sql = "UPDATE ReceiptVouchers SET Date=@Date, BookNo=@BookNo, FromVoucherNo=@FromVoucherNo, ToVoucherNo=@ToVoucherNo, Recipient=@Recipient, Amount=@Amount, Year=@Year, Notes=@Notes WHERE Id=@Id";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", receipt.Id);
                    cmd.Parameters.AddWithValue("@Date", receipt.Date.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@BookNo", receipt.BookNo);
                    cmd.Parameters.AddWithValue("@FromVoucherNo", receipt.FromVoucherNo);
                    cmd.Parameters.AddWithValue("@ToVoucherNo", receipt.ToVoucherNo);
                    cmd.Parameters.AddWithValue("@Recipient", receipt.Recipient);
                    cmd.Parameters.AddWithValue("@Amount", receipt.Amount);
                    cmd.Parameters.AddWithValue("@Year", receipt.Year);
                    cmd.Parameters.AddWithValue("@Notes", (object)receipt.Notes ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SaveDeposit(Deposit deposit)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var sql = "INSERT INTO Deposits (Date, Amount, DepositorName, Year, Notes) VALUES (@Date, @Amount, @DepositorName, @Year, @Notes)";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Date", deposit.Date.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@Amount", deposit.Amount);
                    cmd.Parameters.AddWithValue("@DepositorName", deposit.DepositorName);
                    cmd.Parameters.AddWithValue("@Year", deposit.Year);
                    cmd.Parameters.AddWithValue("@Notes", (object)deposit.Notes ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void UpdateDepositInDb(Deposit deposit)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var sql = "UPDATE Deposits SET Date=@Date, Amount=@Amount, DepositorName=@DepositorName, Year=@Year, Notes=@Notes WHERE Id=@Id";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", deposit.Id);
                    cmd.Parameters.AddWithValue("@Date", deposit.Date.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@Amount", deposit.Amount);
                    cmd.Parameters.AddWithValue("@DepositorName", deposit.DepositorName);
                    cmd.Parameters.AddWithValue("@Year", deposit.Year);
                    cmd.Parameters.AddWithValue("@Notes", (object)deposit.Notes ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void DeleteFromDb(string tableName, int id)
        {
            if (tableName != "ReceiptVouchers" && tableName != "Deposits")
            {
                throw new ArgumentException("جدول غير مسموح حذفه من شاشة الإيرادات.", nameof(tableName));
            }

            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqliteCommand($"DELETE FROM {tableName} WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private List<ReceiptVoucher> LoadReceiptVouchers(string bookNo, string recipient, DateTime? fromDate, DateTime? toDate)
        {
            var list = new List<ReceiptVoucher>();
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var sql = new StringBuilder("SELECT Id, Date, BookNo, FromVoucherNo, ToVoucherNo, Recipient, Amount, Year, Notes FROM ReceiptVouchers WHERE Year = @Year");
                var parameters = new Dictionary<string, object> { { "@Year", FiscalYearHelper.GetSelectedYear(YearComboBox) } };
                if (!string.IsNullOrWhiteSpace(bookNo)) { sql.Append(" AND BookNo LIKE @BookNo"); parameters.Add("@BookNo", "%" + bookNo + "%"); }
                if (!string.IsNullOrWhiteSpace(recipient)) { sql.Append(" AND Recipient LIKE @Recipient"); parameters.Add("@Recipient", "%" + recipient + "%"); }
                if (fromDate.HasValue) { sql.Append(" AND date(Date) >= date(@FromDate)"); parameters.Add("@FromDate", fromDate.Value.ToString("yyyy-MM-dd")); }
                if (toDate.HasValue) { sql.Append(" AND date(Date) <= date(@ToDate)"); parameters.Add("@ToDate", toDate.Value.ToString("yyyy-MM-dd")); }
                using (var cmd = new SqliteCommand(sql.ToString(), conn))
                {
                    foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Key, p.Value);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new ReceiptVoucher
                            {
                                Id = reader.GetInt32(0),
                                Date = DateTime.Parse(reader.GetString(1)),
                                BookNo = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                FromVoucherNo = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                ToVoucherNo = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                                Recipient = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                Amount = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader.GetDouble(6)),
                                Year = reader.GetInt32(7),
                                Notes = reader.IsDBNull(8) ? "" : reader.GetString(8)
                            });
                        }
                    }
                }
            }
            return list;
        }

        private List<Deposit> LoadDeposits(DateTime? fromDate, DateTime? toDate)
        {
            var list = new List<Deposit>();
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var sql = new StringBuilder("SELECT Id, Date, Amount, DepositorName, Year, Notes FROM Deposits WHERE Year = @Year");
                var parameters = new Dictionary<string, object> { { "@Year", FiscalYearHelper.GetSelectedYear(YearComboBox) } };
                if (fromDate.HasValue) { sql.Append(" AND date(Date) >= date(@FromDate)"); parameters.Add("@FromDate", fromDate.Value.ToString("yyyy-MM-dd")); }
                if (toDate.HasValue) { sql.Append(" AND date(Date) <= date(@ToDate)"); parameters.Add("@ToDate", toDate.Value.ToString("yyyy-MM-dd")); }
                using (var cmd = new SqliteCommand(sql.ToString(), conn))
                {
                    foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Key, p.Value);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new Deposit
                            {
                                Id = reader.GetInt32(0),
                                Date = DateTime.Parse(reader.GetString(1)),
                                Amount = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetDouble(2)),
                                DepositorName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                Year = reader.GetInt32(4),
                                Notes = reader.IsDBNull(5) ? "" : reader.GetString(5)
                            });
                        }
                    }
                }
            }
            return list;
        }
        #endregion

        #region UI and Print Helpers
        public void RefreshGrids()
        {
            if (YearComboBox.SelectedItem == null) return;
            string bookNo = SearchBookNoTextBox.Text;
            string recipient = SearchRecipientTextBox.Text;

            DateTime? fromDate = null, toDate = null;
            if (TryParseDate(SearchFromDateTextBox.Text, out DateTime fromDt, false)) fromDate = fromDt;
            if (TryParseDate(SearchToDateTextBox.Text, out DateTime toDt, false)) toDate = toDt;

            ReceiptsDataGrid.ItemsSource = LoadReceiptVouchers(bookNo, recipient, fromDate, toDate);
            DepositsDataGrid.ItemsSource = LoadDeposits(fromDate, toDate);
            UpdateRevenuesBalance();
        }

        private void UpdateRevenuesBalance()
        {
            var receipts = ReceiptsDataGrid.ItemsSource as List<ReceiptVoucher>;
            var deposits = DepositsDataGrid.ItemsSource as List<Deposit>;
            decimal totalReceipts = receipts?.Sum(r => r.Amount) ?? 0;
            decimal totalDeposits = deposits?.Sum(d => d.Amount) ?? 0;
            RevenuesBalanceText.Text = (totalReceipts - totalDeposits).ToString("N3");
        }

        private void ClearReceiptVoucherFields() { ReceiptDateTextBox.Clear(); ReceiptBookNoTextBox.Clear(); ReceiptFromVoucherNoTextBox.Clear(); ReceiptToVoucherNoTextBox.Clear(); ReceiptRecipientTextBox.Clear(); ReceiptAmountTextBox.Clear(); ReceiptNotesTextBox.Clear(); }
        private void ClearDepositFields() { DepositDateTextBox.Clear(); DepositorNameTextBox.Clear(); DepositAmountTextBox.Clear(); DepositNotesTextBox.Clear(); }

        private void EnterEditMode(bool isReceipt)
        {
            if (isReceipt)
            {
                AddUpdateButtonReceipt.Content = "تحديث";
                CancelReceiptEditButton.Visibility = Visibility.Visible;
            }
            else
            {
                AddUpdateButtonDeposit.Content = "تحديث";
                CancelDepositEditButton.Visibility = Visibility.Visible;
            }
        }

        private void ExitEditMode()
        {
            _receiptToEdit = null;
            _depositToEdit = null;
            AddUpdateButtonReceipt.Content = "إضافة سند قبض";
            CancelReceiptEditButton.Visibility = Visibility.Collapsed;
            AddUpdateButtonDeposit.Content = "إضافة إيداع";
            CancelDepositEditButton.Visibility = Visibility.Collapsed;
            ClearReceiptVoucherFields();
            ClearDepositFields();
        }

        private bool TryParseDate(string dateText, out DateTime date, bool showMessage = true)
        {
            return DatabaseService.TryParseDate(
                dateText,
                out date,
                showMessage,
                FiscalYearHelper.GetSelectedYear(YearComboBox));
        }

        private FlowDocument CreateRevenuesPrintDocument(List<ReceiptVoucher> receipts, List<Deposit> deposits)
        {
            var doc = new FlowDocument { FlowDirection = FlowDirection.RightToLeft, FontFamily = new FontFamily("Arial") };
            doc.Blocks.Add(new Paragraph(new Run($"تقرير الإيرادات للسنة المالية {YearComboBox.SelectedItem}")) { FontSize = 20, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 20) });

            if (receipts != null && receipts.Any())
            {
                doc.Blocks.Add(new Paragraph(new Run("سندات القبض")) { FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 10, 0, 5) });
                var receiptsTable = new Table { CellSpacing = 0 };
                doc.Blocks.Add(receiptsTable);
                for (int i = 0; i < 4; i++) receiptsTable.Columns.Add(new TableColumn());
                var headerGroup = new TableRowGroup();
                receiptsTable.RowGroups.Add(headerGroup);
                var headerRow = new TableRow { Background = Brushes.LightGray, FontWeight = FontWeights.Bold };
                headerGroup.Rows.Add(headerRow);
                headerRow.Cells.Add(new TableCell(new Paragraph(new Run("التاريخ"))) { Padding = new Thickness(5) });
                headerRow.Cells.Add(new TableCell(new Paragraph(new Run("رقم الدفتر"))) { Padding = new Thickness(5) });
                headerRow.Cells.Add(new TableCell(new Paragraph(new Run("المستلم"))) { Padding = new Thickness(5) });
                headerRow.Cells.Add(new TableCell(new Paragraph(new Run("المبلغ"))) { Padding = new Thickness(5) });
                var dataGroup = new TableRowGroup();
                receiptsTable.RowGroups.Add(dataGroup);
                foreach (var item in receipts)
                {
                    var dataRow = new TableRow();
                    dataGroup.Rows.Add(dataRow);
                    dataRow.Cells.Add(new TableCell(new Paragraph(new Run(item.Date.ToString(AppSettings.DateFormat)))) { Padding = new Thickness(5), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 0, 1) });
                    dataRow.Cells.Add(new TableCell(new Paragraph(new Run(item.BookNo))) { Padding = new Thickness(5), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 0, 1) });
                    dataRow.Cells.Add(new TableCell(new Paragraph(new Run(item.Recipient))) { Padding = new Thickness(5), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 0, 1) });
                    dataRow.Cells.Add(new TableCell(new Paragraph(new Run(item.Amount.ToString("N3")))) { Padding = new Thickness(5), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 0, 1) });
                }
            }

            if (deposits != null && deposits.Any())
            {
                doc.Blocks.Add(new Paragraph(new Run("الإيداعات")) { FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 20, 0, 5) });
                var depositsTable = new Table { CellSpacing = 0 };
                doc.Blocks.Add(depositsTable);
                for (int i = 0; i < 3; i++) depositsTable.Columns.Add(new TableColumn());
                var headerGroup = new TableRowGroup();
                depositsTable.RowGroups.Add(headerGroup);
                var headerRow = new TableRow { Background = Brushes.LightGray, FontWeight = FontWeights.Bold };
                headerGroup.Rows.Add(headerRow);
                headerRow.Cells.Add(new TableCell(new Paragraph(new Run("تاريخ الإيداع"))) { Padding = new Thickness(5) });
                headerRow.Cells.Add(new TableCell(new Paragraph(new Run("اسم المودع"))) { Padding = new Thickness(5) });
                headerRow.Cells.Add(new TableCell(new Paragraph(new Run("المبلغ"))) { Padding = new Thickness(5) });
                var dataGroup = new TableRowGroup();
                depositsTable.RowGroups.Add(dataGroup);
                foreach (var item in deposits)
                {
                    var dataRow = new TableRow();
                    dataGroup.Rows.Add(dataRow);
                    dataRow.Cells.Add(new TableCell(new Paragraph(new Run(item.Date.ToString(AppSettings.DateFormat)))) { Padding = new Thickness(5), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 0, 1) });
                    dataRow.Cells.Add(new TableCell(new Paragraph(new Run(item.DepositorName))) { Padding = new Thickness(5), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 0, 1) });
                    dataRow.Cells.Add(new TableCell(new Paragraph(new Run(item.Amount.ToString("N3")))) { Padding = new Thickness(5), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 0, 1) });
                }
            }

            doc.Blocks.Add(new Paragraph(new Run("الملخص المالي")) { FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 30, 0, 5) });
            decimal totalReceipts = receipts?.Sum(r => r.Amount) ?? 0;
            decimal totalDeposits = deposits?.Sum(d => d.Amount) ?? 0;
            doc.Blocks.Add(new Paragraph(new Run($"إجمالي سندات القبض: {totalReceipts:N3}")));
            doc.Blocks.Add(new Paragraph(new Run($"إجمالي الإيداعات: {totalDeposits:N3}")));
            doc.Blocks.Add(new Paragraph(new Run($"الرصيد النهائي: {(totalReceipts - totalDeposits):N3}")) { FontWeight = FontWeights.Bold });
            return doc;
        }

        // --- دوال معالجة التاريخ الجديدة ---

        private void DateInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TextBox txt = sender as TextBox;
                if (txt != null && !string.IsNullOrWhiteSpace(txt.Text))
                {
                    // 1. تنسيق التاريخ وإكماله بناءً على السنة المختارة
                    txt.Text = FormatDateString(txt.Text);

                    // 2. نقل التركيز للحقل التالي
                    e.Handled = true;
                    TraversalRequest request = new TraversalRequest(FocusNavigationDirection.Next);
                    request.Wrapped = true;
                    ((UIElement)sender).MoveFocus(request);
                }
            }
        }

        private string FormatDateString(string input)
        {
            try
            {
                input = input.Trim();
                string[] parts = input.Split(new char[] { '/', '.', '-' }, StringSplitOptions.RemoveEmptyEntries);

                int day, month, year;

                // الحصول على السنة من الكومبو بوكس
                int selectedYear = DateTime.Now.Year;
                if (YearComboBox.SelectedItem != null)
                {
                    int.TryParse(YearComboBox.SelectedItem.ToString(), out selectedYear);
                }

                if (parts.Length == 2)
                {
                    // الحالة: يوم/شهر -> نستخدم السنة المختارة
                    day = int.Parse(parts[0]);
                    month = int.Parse(parts[1]);
                    year = selectedYear;
                }
                else if (parts.Length == 3)
                {
                    day = int.Parse(parts[0]);
                    month = int.Parse(parts[1]);
                    year = int.Parse(parts[2]);
                    if (year < 100) year += 2000;
                }
                else if (parts.Length == 1)
                {
                    // الحالة: يوم فقط
                    day = int.Parse(parts[0]);
                    month = DateTime.Now.Month;
                    year = selectedYear;
                }
                else
                {
                    return input;
                }

                DateTime dt = new DateTime(year, month, day);
                // استخدام التنسيق الموحد من إعدادات التطبيق لإزالة التعارض
                return dt.ToString(AppSettings.DateFormat);
            }
            catch
            {
                return input;
            }
        }

        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
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
