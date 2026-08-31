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
        private ReceiptVoucher _receiptToEdit;
        private Deposit _depositToEdit;

        public RevenuesView()
        {
            InitializeComponent();
            DatabaseService.InitializeDatabase();
        }

        #region Event Handlers

        private void RevenuesView_Loaded(object sender, RoutedEventArgs e)
        {
            FiscalYearHelper.SelectCurrentYear(YearComboBox);
            ResetDateFieldsToSelectedYear();
            RefreshGrids();
        }

        private void YearComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            ExitEditMode();
            ResetDateFieldsToSelectedYear();
            RefreshGrids();
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            RefreshGrids();
        }

        private void ShowAll_Click(object sender, RoutedEventArgs e)
        {
            SearchBookNoTextBox.Clear();
            SearchRecipientTextBox.Clear();
            ResetDateFieldsToSelectedYear();
            RefreshGrids();
        }

        private void AddUpdateReceiptVoucher_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseDate(ReceiptDateTextBox.Text, out DateTime date)) return;
            if (!FiscalYearHelper.ValidateDateInSelectedYear(date, YearComboBox, "تاريخ سند القبض")) return;
            if (!TryParsePositiveAmount(ReceiptAmountTextBox.Text, "مبلغ سند القبض", out decimal amount)) return;
            if (!TryParseOptionalPositiveInt(ReceiptFromVoucherNoTextBox.Text, "رقم بداية السند", out int fromVoucherNo)) return;
            if (!TryParseOptionalPositiveInt(ReceiptToVoucherNoTextBox.Text, "رقم نهاية السند", out int toVoucherNo)) return;

            if (fromVoucherNo > 0 && toVoucherNo > 0 && toVoucherNo < fromVoucherNo)
            {
                MessageBox.Show(
                    "رقم (إلى سند) يجب أن يكون مساوياً أو أكبر من رقم (من سند).",
                    "أرقام السندات غير صحيحة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var voucher = new ReceiptVoucher
            {
                Date = date,
                BookNo = ReceiptBookNoTextBox.Text?.Trim(),
                FromVoucherNo = fromVoucherNo,
                ToVoucherNo = toVoucherNo,
                Recipient = ReceiptRecipientTextBox.Text?.Trim(),
                Amount = amount,
                Year = FiscalYearHelper.GetSelectedYear(YearComboBox),
                Notes = ReceiptNotesTextBox.Text?.Trim()
            };

            if (_receiptToEdit == null)
            {
                SaveReceiptVoucher(voucher);
            }
            else
            {
                voucher.Id = _receiptToEdit.Id;
                UpdateReceiptVoucherInDb(voucher);
            }

            ExitEditMode();
            RefreshGrids();
        }

        private void AddUpdateDeposit_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseDate(DepositDateTextBox.Text, out DateTime date)) return;
            if (!FiscalYearHelper.ValidateDateInSelectedYear(date, YearComboBox, "تاريخ الإيداع")) return;
            if (!TryParsePositiveAmount(DepositAmountTextBox.Text, "مبلغ الإيداع", out decimal amount)) return;

            var deposit = new Deposit
            {
                Date = date,
                DepositorName = DepositorNameTextBox.Text?.Trim(),
                Amount = amount,
                Year = FiscalYearHelper.GetSelectedYear(YearComboBox),
                Notes = DepositNotesTextBox.Text?.Trim()
            };

            if (_depositToEdit == null)
            {
                SaveDeposit(deposit);
            }
            else
            {
                deposit.Id = _depositToEdit.Id;
                UpdateDepositInDb(deposit);
            }

            ExitEditMode();
            RefreshGrids();
        }

        private void EditReceipt_Click(object sender, RoutedEventArgs e)
        {
            if (!(ReceiptsDataGrid.SelectedItem is ReceiptVoucher selected)) return;

            _receiptToEdit = selected;
            _depositToEdit = null;
            ReceiptDateTextBox.Text = selected.Date.ToString(AppSettings.DateFormat);
            ReceiptBookNoTextBox.Text = selected.BookNo;
            ReceiptFromVoucherNoTextBox.Text = selected.FromVoucherNo > 0 ? selected.FromVoucherNo.ToString() : string.Empty;
            ReceiptToVoucherNoTextBox.Text = selected.ToVoucherNo > 0 ? selected.ToVoucherNo.ToString() : string.Empty;
            ReceiptRecipientTextBox.Text = selected.Recipient;
            ReceiptAmountTextBox.Text = selected.Amount.ToString("0.###");
            ReceiptNotesTextBox.Text = selected.Notes;
            EnterEditMode(true);
        }

        private void EditDeposit_Click(object sender, RoutedEventArgs e)
        {
            if (!(DepositsDataGrid.SelectedItem is Deposit selected)) return;

            _depositToEdit = selected;
            _receiptToEdit = null;
            DepositDateTextBox.Text = selected.Date.ToString(AppSettings.DateFormat);
            DepositorNameTextBox.Text = selected.DepositorName;
            DepositAmountTextBox.Text = selected.Amount.ToString("0.###");
            DepositNotesTextBox.Text = selected.Notes;
            EnterEditMode(false);
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            ExitEditMode();
        }

        private void DeleteReceipt_Click(object sender, RoutedEventArgs e)
        {
            if (!(ReceiptsDataGrid.SelectedItem is ReceiptVoucher selected)) return;

            if (MessageBox.Show(
                    "هل أنت متأكد من حذف هذا السند؟",
                    "تأكيد الحذف",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            DeleteFromDb("ReceiptVouchers", selected.Id);
            RefreshGrids();
        }

        private void DeleteDeposit_Click(object sender, RoutedEventArgs e)
        {
            if (!(DepositsDataGrid.SelectedItem is Deposit selected)) return;

            if (MessageBox.Show(
                    "هل أنت متأكد من حذف هذا الإيداع؟",
                    "تأكيد الحذف",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            DeleteFromDb("Deposits", selected.Id);
            RefreshGrids();
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

            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true) return;

            FlowDocument doc = CreateRevenuesPrintDocument(receipts, deposits);
            doc.PagePadding = new Thickness(50);
            doc.ColumnWidth = printDialog.PrintableAreaWidth;
            printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "تقرير الإيرادات");
        }

        private void InputTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InputTabControl != null && DataTabControl != null && e.Source == InputTabControl)
            {
                DataTabControl.SelectedIndex = InputTabControl.SelectedIndex;
            }
        }

        private void DataTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InputTabControl != null && DataTabControl != null && e.Source == DataTabControl)
            {
                InputTabControl.SelectedIndex = DataTabControl.SelectedIndex;
            }
        }

        #endregion

        #region Validation

        private bool TryParsePositiveAmount(string text, string fieldName, out decimal amount)
        {
            amount = 0;
            string value = text?.Trim();

            bool parsed = decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out amount) ||
                          decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);

            if (!parsed || amount <= 0)
            {
                MessageBox.Show(
                    $"الرجاء إدخال {fieldName} بشكل صحيح وأن يكون أكبر من صفر.",
                    "مبلغ غير صحيح",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool TryParseOptionalPositiveInt(string text, string fieldName, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return true;

            if (!int.TryParse(text.Trim(), out value) || value <= 0)
            {
                MessageBox.Show(
                    $"الرجاء إدخال {fieldName} كرقم صحيح أكبر من صفر، أو اترك الحقل فارغاً.",
                    "رقم سند غير صحيح",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool TryParseDate(string dateText, out DateTime date, bool showMessage = true)
        {
            return DatabaseService.TryParseDate(
                dateText,
                out date,
                showMessage,
                FiscalYearHelper.GetSelectedYear(YearComboBox));
        }

        private bool TryParseOptionalSearchDate(string text, string fieldName, out DateTime? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(text)) return true;

            if (!TryParseDate(text, out DateTime parsedDate, true)) return false;
            if (!FiscalYearHelper.ValidateDateInSelectedYear(parsedDate, YearComboBox, fieldName)) return false;

            value = parsedDate;
            return true;
        }

        #endregion

        #region Database Interaction

        private void SaveReceiptVoucher(ReceiptVoucher receipt)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                const string sql = "INSERT INTO ReceiptVouchers (Date, BookNo, FromVoucherNo, ToVoucherNo, Recipient, Amount, Year, Notes) VALUES (@Date, @BookNo, @FromVoucherNo, @ToVoucherNo, @Recipient, @Amount, @Year, @Notes)";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    AddReceiptParameters(cmd, receipt);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void UpdateReceiptVoucherInDb(ReceiptVoucher receipt)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                const string sql = "UPDATE ReceiptVouchers SET Date=@Date, BookNo=@BookNo, FromVoucherNo=@FromVoucherNo, ToVoucherNo=@ToVoucherNo, Recipient=@Recipient, Amount=@Amount, Year=@Year, Notes=@Notes WHERE Id=@Id";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", receipt.Id);
                    AddReceiptParameters(cmd, receipt);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void AddReceiptParameters(SqliteCommand cmd, ReceiptVoucher receipt)
        {
            cmd.Parameters.AddWithValue("@Date", receipt.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@BookNo", (object)receipt.BookNo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FromVoucherNo", receipt.FromVoucherNo);
            cmd.Parameters.AddWithValue("@ToVoucherNo", receipt.ToVoucherNo);
            cmd.Parameters.AddWithValue("@Recipient", (object)receipt.Recipient ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Amount", receipt.Amount);
            cmd.Parameters.AddWithValue("@Year", receipt.Year);
            cmd.Parameters.AddWithValue("@Notes", (object)receipt.Notes ?? DBNull.Value);
        }

        private void SaveDeposit(Deposit deposit)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                const string sql = "INSERT INTO Deposits (Date, Amount, DepositorName, Year, Notes) VALUES (@Date, @Amount, @DepositorName, @Year, @Notes)";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    AddDepositParameters(cmd, deposit);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void UpdateDepositInDb(Deposit deposit)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                const string sql = "UPDATE Deposits SET Date=@Date, Amount=@Amount, DepositorName=@DepositorName, Year=@Year, Notes=@Notes WHERE Id=@Id";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", deposit.Id);
                    AddDepositParameters(cmd, deposit);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void AddDepositParameters(SqliteCommand cmd, Deposit deposit)
        {
            cmd.Parameters.AddWithValue("@Date", deposit.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@Amount", deposit.Amount);
            cmd.Parameters.AddWithValue("@DepositorName", (object)deposit.DepositorName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Year", deposit.Year);
            cmd.Parameters.AddWithValue("@Notes", (object)deposit.Notes ?? DBNull.Value);
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
                var parameters = new Dictionary<string, object>
                {
                    { "@Year", FiscalYearHelper.GetSelectedYear(YearComboBox) }
                };

                if (!string.IsNullOrWhiteSpace(bookNo))
                {
                    sql.Append(" AND BookNo LIKE @BookNo");
                    parameters.Add("@BookNo", "%" + bookNo.Trim() + "%");
                }
                if (!string.IsNullOrWhiteSpace(recipient))
                {
                    sql.Append(" AND Recipient LIKE @Recipient");
                    parameters.Add("@Recipient", "%" + recipient.Trim() + "%");
                }
                if (fromDate.HasValue)
                {
                    sql.Append(" AND date(Date) >= date(@FromDate)");
                    parameters.Add("@FromDate", fromDate.Value.ToString("yyyy-MM-dd"));
                }
                if (toDate.HasValue)
                {
                    sql.Append(" AND date(Date) <= date(@ToDate)");
                    parameters.Add("@ToDate", toDate.Value.ToString("yyyy-MM-dd"));
                }

                sql.Append(" ORDER BY date(Date), Id");

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
                                Date = ParseStoredDate(reader.GetString(1)),
                                BookNo = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                FromVoucherNo = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                ToVoucherNo = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                                Recipient = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                                Amount = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader.GetDouble(6)),
                                Year = reader.GetInt32(7),
                                Notes = reader.IsDBNull(8) ? string.Empty : reader.GetString(8)
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
                var parameters = new Dictionary<string, object>
                {
                    { "@Year", FiscalYearHelper.GetSelectedYear(YearComboBox) }
                };

                if (fromDate.HasValue)
                {
                    sql.Append(" AND date(Date) >= date(@FromDate)");
                    parameters.Add("@FromDate", fromDate.Value.ToString("yyyy-MM-dd"));
                }
                if (toDate.HasValue)
                {
                    sql.Append(" AND date(Date) <= date(@ToDate)");
                    parameters.Add("@ToDate", toDate.Value.ToString("yyyy-MM-dd"));
                }

                sql.Append(" ORDER BY date(Date), Id");

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
                                Date = ParseStoredDate(reader.GetString(1)),
                                Amount = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetDouble(2)),
                                DepositorName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                Year = reader.GetInt32(4),
                                Notes = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                            });
                        }
                    }
                }
            }
            return list;
        }

        private decimal LoadFullYearBalance()
        {
            int year = FiscalYearHelper.GetSelectedYear(YearComboBox);
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                const string sql = @"SELECT
                    COALESCE((SELECT SUM(Amount) FROM ReceiptVouchers WHERE Year = @Year), 0),
                    COALESCE((SELECT SUM(Amount) FROM Deposits WHERE Year = @Year), 0)";

                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@Year", year);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return 0;
                        decimal receipts = reader.IsDBNull(0) ? 0 : Convert.ToDecimal(reader.GetDouble(0));
                        decimal deposits = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetDouble(1));
                        return receipts - deposits;
                    }
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

        #region UI and Print Helpers

        public void RefreshGrids()
        {
            if (YearComboBox.SelectedItem == null) return;

            if (!TryParseOptionalSearchDate(SearchFromDateTextBox.Text, "تاريخ بداية البحث", out DateTime? fromDate)) return;
            if (!TryParseOptionalSearchDate(SearchToDateTextBox.Text, "تاريخ نهاية البحث", out DateTime? toDate)) return;

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            {
                MessageBox.Show(
                    "تاريخ بداية البحث يجب أن يكون قبل أو مساوياً لتاريخ النهاية.",
                    "نطاق تاريخ غير صحيح",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            ReceiptsDataGrid.ItemsSource = LoadReceiptVouchers(
                SearchBookNoTextBox.Text,
                SearchRecipientTextBox.Text,
                fromDate,
                toDate);

            DepositsDataGrid.ItemsSource = LoadDeposits(fromDate, toDate);

            // هذه البطاقة تمثل رصيد السنة المالية كاملاً، ولا تتغير بسبب فلتر اسم المستلم أو رقم الدفتر.
            RevenuesBalanceText.Text = LoadFullYearBalance().ToString("N3");
        }

        private void ResetDateFieldsToSelectedYear()
        {
            FiscalYearHelper.ResetDateRange(YearComboBox, SearchFromDateTextBox, SearchToDateTextBox);
        }

        private void ClearReceiptVoucherFields()
        {
            ReceiptDateTextBox.Clear();
            ReceiptBookNoTextBox.Clear();
            ReceiptFromVoucherNoTextBox.Clear();
            ReceiptToVoucherNoTextBox.Clear();
            ReceiptRecipientTextBox.Clear();
            ReceiptAmountTextBox.Clear();
            ReceiptNotesTextBox.Clear();
        }

        private void ClearDepositFields()
        {
            DepositDateTextBox.Clear();
            DepositorNameTextBox.Clear();
            DepositAmountTextBox.Clear();
            DepositNotesTextBox.Clear();
        }

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

        private FlowDocument CreateRevenuesPrintDocument(List<ReceiptVoucher> receipts, List<Deposit> deposits)
        {
            var doc = new FlowDocument
            {
                FlowDirection = FlowDirection.RightToLeft,
                FontFamily = new FontFamily("Arial")
            };

            doc.Blocks.Add(new Paragraph(new Run($"تقرير الإيرادات للسنة المالية {YearComboBox.SelectedItem}"))
            {
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            });

            if (receipts != null && receipts.Any())
            {
                doc.Blocks.Add(new Paragraph(new Run("سندات القبض المعروضة"))
                {
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 10, 0, 5)
                });
                doc.Blocks.Add(CreateReceiptsPrintTable(receipts));
            }

            if (deposits != null && deposits.Any())
            {
                doc.Blocks.Add(new Paragraph(new Run("الإيداعات المعروضة"))
                {
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 20, 0, 5)
                });
                doc.Blocks.Add(CreateDepositsPrintTable(deposits));
            }

            decimal displayedReceipts = receipts?.Sum(r => r.Amount) ?? 0;
            decimal displayedDeposits = deposits?.Sum(d => d.Amount) ?? 0;

            doc.Blocks.Add(new Paragraph(new Run("ملخص البيانات المعروضة"))
            {
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 30, 0, 5)
            });
            doc.Blocks.Add(new Paragraph(new Run($"إجمالي سندات القبض المعروضة: {displayedReceipts:N3}")));
            doc.Blocks.Add(new Paragraph(new Run($"إجمالي الإيداعات المعروضة: {displayedDeposits:N3}")));
            doc.Blocks.Add(new Paragraph(new Run($"رصيد الإيرادات الكامل للسنة المالية: {LoadFullYearBalance():N3}"))
            {
                FontWeight = FontWeights.Bold
            });

            return doc;
        }

        private static Table CreateReceiptsPrintTable(IEnumerable<ReceiptVoucher> receipts)
        {
            var table = CreatePrintTable(new[] { "التاريخ", "رقم الدفتر", "المستلم", "المبلغ" });
            var group = new TableRowGroup();
            table.RowGroups.Add(group);

            foreach (var item in receipts)
            {
                var row = new TableRow();
                group.Rows.Add(row);
                row.Cells.Add(CreatePrintCell(item.Date.ToString(AppSettings.DateFormat)));
                row.Cells.Add(CreatePrintCell(item.BookNo));
                row.Cells.Add(CreatePrintCell(item.Recipient));
                row.Cells.Add(CreatePrintCell(item.Amount.ToString("N3")));
            }

            return table;
        }

        private static Table CreateDepositsPrintTable(IEnumerable<Deposit> deposits)
        {
            var table = CreatePrintTable(new[] { "تاريخ الإيداع", "اسم المودع", "المبلغ" });
            var group = new TableRowGroup();
            table.RowGroups.Add(group);

            foreach (var item in deposits)
            {
                var row = new TableRow();
                group.Rows.Add(row);
                row.Cells.Add(CreatePrintCell(item.Date.ToString(AppSettings.DateFormat)));
                row.Cells.Add(CreatePrintCell(item.DepositorName));
                row.Cells.Add(CreatePrintCell(item.Amount.ToString("N3")));
            }

            return table;
        }

        private static Table CreatePrintTable(IEnumerable<string> headers)
        {
            var headerList = headers.ToList();
            var table = new Table { CellSpacing = 0 };
            foreach (string unused in headerList) table.Columns.Add(new TableColumn());

            var headerGroup = new TableRowGroup();
            table.RowGroups.Add(headerGroup);
            var headerRow = new TableRow
            {
                Background = Brushes.LightGray,
                FontWeight = FontWeights.Bold
            };
            headerGroup.Rows.Add(headerRow);

            foreach (string header in headerList)
            {
                headerRow.Cells.Add(CreatePrintCell(header, true));
            }

            return table;
        }

        private static TableCell CreatePrintCell(string text, bool isHeader = false)
        {
            var cell = new TableCell(new Paragraph(new Run(text ?? string.Empty)))
            {
                Padding = new Thickness(5)
            };

            if (!isHeader)
            {
                cell.BorderBrush = Brushes.Gainsboro;
                cell.BorderThickness = new Thickness(0, 0, 0, 1);
            }

            return cell;
        }

        private void DateInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            var textBox = sender as TextBox;
            if (textBox != null && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                if (!TryParseDate(textBox.Text, out DateTime parsedDate, true))
                {
                    e.Handled = true;
                    return;
                }

                textBox.Text = parsedDate.ToString(AppSettings.DateFormat);
            }

            MoveFocusOnEnter(sender, e);
        }

        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            var request = new TraversalRequest(FocusNavigationDirection.Next);
            if (Keyboard.FocusedElement is UIElement elementWithFocus)
            {
                elementWithFocus.MoveFocus(request);
            }
            e.Handled = true;
        }

        #endregion
    }
}
