using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Printing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace AccountingApp
{
    public partial class AidsView : UserControl
    {
        private readonly string _connectionString = DatabaseService.ConnectionString;
        private AidEntry _aidToEdit;
        private TextBox _voucherNoTextBox;
        private TextBox _searchVoucherNoTextBox;

        private readonly List<string> _projectNames = new List<string>
        {
            "الطرود الغذائية", "الملابس والأحذية", "معونة الشتاء", "الحقيبة المدرسية", "كسوة العيد", "إفطار صائم", "الأضاحي",
            "أثاث منازل للفقراء", "مواد مستهلكة للفقراء", "نذور وكفارات", "أصول ثابتة", "مواد مستهلكة للمركز", "أدوية ومستلزمات طبية"
        };

        private class AidFieldSet
        {
            public TextBox Date { get; set; }
            public TextBox Donor { get; set; }
            public TextBox Type { get; set; }
            public TextBox Quantity { get; set; }
            public TextBox Amount { get; set; }
            public Button SubmitButton { get; set; }
        }

        public AidsView()
        {
            InitializeComponent();
            DatabaseService.InitializeDatabase();
            InitializeVoucherUi();

            for (int i = 0; i < _projectNames.Count && i < AidEntryTabControl.Items.Count; i++)
            {
                ((TabItem)AidEntryTabControl.Items[i]).Header = _projectNames[i];
            }

            PopulateComboBoxes();
        }

        private void InitializeVoucherUi()
        {
            InitializeVoucherEntryUi();
            InitializeVoucherSearchUi();

            if (!AidsDataGrid.Columns.Any(c => string.Equals(c.Header?.ToString(), "رقم السند", StringComparison.Ordinal)))
            {
                AidsDataGrid.Columns.Insert(1, new DataGridTextColumn
                {
                    Header = "رقم السند",
                    Binding = new Binding(nameof(AidEntry.VoucherNo)),
                    Width = new DataGridLength(110)
                });
            }
        }

        private void InitializeVoucherEntryUi()
        {
            var groupBox = AidEntryTabControl.Parent as GroupBox;
            if (groupBox == null) return;

            groupBox.Content = null;

            var wrapper = new StackPanel();
            var voucherRow = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(247, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 224)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(5, 5, 5, 10)
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock
            {
                Text = "رقم السند:",
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });

            _voucherNoTextBox = new TextBox
            {
                Width = 180,
                Height = 28,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _voucherNoTextBox.KeyDown += MoveFocusOnEnter;
            row.Children.Add(_voucherNoTextBox);
            row.Children.Add(new TextBlock
            {
                Text = "  مطلوب لكل إدخال جديد",
                Foreground = Brushes.DimGray,
                VerticalAlignment = VerticalAlignment.Center
            });

            voucherRow.Child = row;
            wrapper.Children.Add(voucherRow);
            wrapper.Children.Add(AidEntryTabControl);
            groupBox.Content = wrapper;
        }

        private void InitializeVoucherSearchUi()
        {
            var searchGrid = SearchProjectComboBox.Parent as Grid;
            var groupBox = searchGrid?.Parent as GroupBox;
            if (searchGrid == null || groupBox == null) return;

            groupBox.Content = null;
            var wrapper = new StackPanel();
            wrapper.Children.Add(searchGrid);

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 0)
            };
            row.Children.Add(new TextBlock
            {
                Text = "رقم السند:",
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });

            _searchVoucherNoTextBox = new TextBox
            {
                Width = 180,
                Height = 28,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _searchVoucherNoTextBox.KeyDown += MoveFocusOnEnter;
            row.Children.Add(_searchVoucherNoTextBox);
            row.Children.Add(new TextBlock
            {
                Text = "  يمكن كتابة جزء من رقم السند ثم الضغط على بحث",
                Foreground = Brushes.DimGray,
                VerticalAlignment = VerticalAlignment.Center
            });

            wrapper.Children.Add(row);
            groupBox.Content = wrapper;
        }

        private void PopulateComboBoxes()
        {
            FiscalYearHelper.SelectCurrentYear(YearComboBox);
            var searchProjects = new List<string> { "جميع المشاريع" };
            searchProjects.AddRange(_projectNames);
            SearchProjectComboBox.ItemsSource = searchProjects;
            SearchProjectComboBox.SelectedItem = "جميع المشاريع";
        }

        #region Event Handlers
        private void AidsView_Loaded(object sender, RoutedEventArgs e)
        {
            FiscalYearHelper.SelectCurrentYear(YearComboBox);
            ResetDateFieldsToSelectedYear();
            RefreshSummaryGrid(false);
            LoadDetailsForCurrentProject();
        }

        private void YearComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            CancelCurrentEditAndClear();
            ResetDateFieldsToSelectedYear();
            RefreshSummaryGrid(false);
            LoadDetailsForCurrentProject();
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            RefreshSummaryGrid(true);
        }

        private void ShowAll_Click(object sender, RoutedEventArgs e)
        {
            SearchProjectComboBox.SelectedItem = "جميع المشاريع";
            _searchVoucherNoTextBox?.Clear();
            ResetDateFieldsToSelectedYear();
            RefreshSummaryGrid(false);
        }

        private void ProjectsSummaryDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(ProjectsSummaryDataGrid.SelectedItem is ProjectSummary selectedProject))
            {
                AidsDataGrid.ItemsSource = null;
                UpdateDetailsColumns(null);
                return;
            }

            foreach (TabItem tab in AidEntryTabControl.Items)
            {
                if (string.Equals(tab.Header?.ToString(), selectedProject.ProjectName, StringComparison.Ordinal))
                {
                    AidEntryTabControl.SelectedItem = tab;
                    break;
                }
            }

            if (SearchProjectComboBox != null)
            {
                SearchProjectComboBox.SelectedItem = selectedProject.ProjectName;
            }

            if (!TryGetSearchDateRange(false, out DateTime? fromDate, out DateTime? toDate)) return;
            AidsDataGrid.ItemsSource = LoadAidDetails(selectedProject.ProjectName, GetVoucherSearchText(), fromDate, toDate);
            UpdateDetailsColumns(selectedProject.ProjectName);
        }

        private void LoadDetailsForCurrentProject()
        {
            if (!(AidEntryTabControl.SelectedItem is TabItem selectedTab)) return;
            string projectName = selectedTab.Header?.ToString();
            if (string.IsNullOrWhiteSpace(projectName)) return;

            if (!TryGetSearchDateRange(false, out DateTime? fromDate, out DateTime? toDate)) return;
            AidsDataGrid.ItemsSource = LoadAidDetails(projectName, GetVoucherSearchText(), fromDate, toDate);
            UpdateDetailsColumns(projectName);
        }

        private void AidEntryTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || !(AidEntryTabControl.SelectedItem is TabItem selectedTab)) return;
            string projectName = selectedTab.Header?.ToString();

            if (_aidToEdit != null && !string.Equals(_aidToEdit.ProjectName, projectName, StringComparison.Ordinal))
            {
                string oldProject = _aidToEdit.ProjectName;
                ExitEditMode();
                ClearProjectFields(oldProject);
                _voucherNoTextBox?.Clear();
            }

            var summaryItem = ProjectsSummaryDataGrid.Items
                .OfType<ProjectSummary>()
                .FirstOrDefault(p => p.ProjectName == projectName);

            if (summaryItem != null)
            {
                ProjectsSummaryDataGrid.SelectedItem = summaryItem;
            }
            else
            {
                LoadDetailsForCurrentProject();
            }

            UpdateDetailsColumns(projectName);
        }

        private void AidsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void AddAid_Click(object sender, RoutedEventArgs e)
        {
            if (!(AidEntryTabControl.SelectedItem is TabItem selectedTab)) return;
            string projectName = selectedTab.Header?.ToString();
            if (string.IsNullOrWhiteSpace(projectName)) return;

            var entry = new AidEntry
            {
                ProjectName = projectName,
                Year = FiscalYearHelper.GetSelectedYear(YearComboBox)
            };

            if (!PopulateAidEntry(entry, projectName)) return;
            if (!FiscalYearHelper.ValidateDateInSelectedYear(entry.Date, YearComboBox, "تاريخ المساعدة")) return;

            bool saved;
            if (_aidToEdit == null)
            {
                saved = SaveAidEntry(entry);
                if (saved)
                {
                    MessageBox.Show($"تمت إضافة الإدخال بنجاح.\nرقم السند: {entry.VoucherNo}", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                entry.Id = _aidToEdit.Id;
                saved = UpdateAidEntry(entry);
                if (saved)
                {
                    MessageBox.Show($"تم تحديث الإدخال بنجاح.\nرقم السند: {entry.VoucherNo}", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }

            if (!saved) return;

            ExitEditMode();
            ClearProjectFields(projectName);
            _voucherNoTextBox?.Clear();
            RefreshSummaryGrid(false);
            LoadDetailsForCurrentProject();
        }

        private void EditAid_Click(object sender, RoutedEventArgs e)
        {
            var selectedAid = (sender as FrameworkElement)?.DataContext as AidEntry;
            if (selectedAid == null)
            {
                MessageBox.Show("لم يتم العثور على بيانات السجل المحدد.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _aidToEdit = selectedAid;

            foreach (TabItem tab in AidEntryTabControl.Items)
            {
                if (string.Equals(tab.Header?.ToString(), selectedAid.ProjectName, StringComparison.Ordinal))
                {
                    AidEntryTabControl.SelectedItem = tab;
                    break;
                }
            }

            PopulateUIForEdit(selectedAid);
            SetButtonContent(selectedAid.ProjectName, "تحديث الإدخال");
        }

        private void DeleteAid_Click(object sender, RoutedEventArgs e)
        {
            var selectedAid = (sender as FrameworkElement)?.DataContext as AidEntry;
            if (selectedAid == null) return;

            string voucherInfo = string.IsNullOrWhiteSpace(selectedAid.VoucherNo)
                ? string.Empty
                : $"\nرقم السند: {selectedAid.VoucherNo}";

            if (MessageBox.Show(
                $"هل أنت متأكد من رغبتك في حذف هذا السجل؟{voucherInfo}\nسيؤثر الحذف على ملخص المشروع والتقارير.",
                "تأكيد الحذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            if (!DeleteAidEntry(selectedAid.Id)) return;

            if (_aidToEdit?.Id == selectedAid.Id)
            {
                CancelCurrentEditAndClear();
            }

            RefreshSummaryGrid(false);
            LoadDetailsForCurrentProject();
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSearchDateRange(true, out DateTime? fromDate, out DateTime? toDate)) return;

            string filterProject = SearchProjectComboBox.SelectedItem as string;
            if (filterProject == "جميع المشاريع") filterProject = null;

            var allEntries = LoadAllAidDetailsForPrinting(filterProject, GetVoucherSearchText(), fromDate, toDate);
            if (!allEntries.Any())
            {
                MessageBox.Show("لا توجد بيانات للطباعة.");
                return;
            }

            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                FlowDocument doc = CreatePrintDocument(allEntries, filterProject, fromDate, toDate);
                doc.PagePadding = new Thickness(40);
                doc.ColumnWidth = printDialog.PrintableAreaWidth;
                printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "تقرير المساعدات");
            }
        }
        #endregion

        #region Database Interaction
        private bool SaveAidEntry(AidEntry entry)
        {
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    const string sql = @"INSERT INTO Aids
                        (ProjectName, VoucherNo, DonorName, Date, Amount, Quantity, DonationType, Year)
                        VALUES (@ProjectName, @VoucherNo, @DonorName, @Date, @Amount, @Quantity, @DonationType, @Year)";
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        AddAidParameters(cmd, entry, false);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (SqliteException)
            {
                MessageBox.Show("تعذر حفظ بيانات المساعدة في قاعدة البيانات.", "خطأ في الحفظ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool UpdateAidEntry(AidEntry entry)
        {
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    const string sql = @"UPDATE Aids SET
                        ProjectName = @ProjectName,
                        VoucherNo = @VoucherNo,
                        DonorName = @DonorName,
                        Date = @Date,
                        Amount = @Amount,
                        Quantity = @Quantity,
                        DonationType = @DonationType,
                        Year = @Year
                        WHERE Id = @Id";
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        AddAidParameters(cmd, entry, true);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (SqliteException)
            {
                MessageBox.Show("تعذر تحديث بيانات المساعدة في قاعدة البيانات.", "خطأ في التحديث", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private static void AddAidParameters(SqliteCommand cmd, AidEntry entry, bool includeId)
        {
            cmd.Parameters.AddWithValue("@ProjectName", entry.ProjectName);
            cmd.Parameters.AddWithValue("@VoucherNo", (object)entry.VoucherNo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DonorName", (object)entry.DonorName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Date", entry.Date.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@Amount", entry.Amount);
            cmd.Parameters.AddWithValue("@Quantity", entry.Quantity);
            cmd.Parameters.AddWithValue("@DonationType", (object)entry.DonationType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Year", entry.Year);
            if (includeId) cmd.Parameters.AddWithValue("@Id", entry.Id);
        }

        private bool DeleteAidEntry(int id)
        {
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqliteCommand("DELETE FROM Aids WHERE Id = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (SqliteException)
            {
                MessageBox.Show("تعذر حذف السجل من قاعدة البيانات.", "خطأ في الحذف", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private List<ProjectSummary> LoadProjectsSummary(string filterProject, string voucherFilter, DateTime? fromDate, DateTime? toDate)
        {
            var list = new List<ProjectSummary>();
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                int selectedYear = FiscalYearHelper.GetSelectedYear(YearComboBox);
                var sql = new StringBuilder("SELECT ProjectName, COALESCE(SUM(Amount),0), COALESCE(SUM(Quantity),0) FROM Aids WHERE Year = @Year");
                var parameters = new Dictionary<string, object> { { "@Year", selectedYear } };

                AppendAidFilters(sql, parameters, filterProject, voucherFilter, fromDate, toDate);
                sql.Append(" GROUP BY ProjectName ORDER BY ProjectName");

                using (var cmd = new SqliteCommand(sql.ToString(), conn))
                {
                    foreach (var parameter in parameters) cmd.Parameters.AddWithValue(parameter.Key, parameter.Value);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new ProjectSummary
                            {
                                ProjectName = reader.GetString(0),
                                TotalAmount = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1)),
                                TotalQuantity = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2))
                            });
                        }
                    }
                }
            }
            return list;
        }

        private List<AidEntry> LoadAidDetails(string projectName, string voucherFilter, DateTime? fromDate, DateTime? toDate)
        {
            var list = new List<AidEntry>();
            if (string.IsNullOrWhiteSpace(projectName)) return list;

            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                int selectedYear = FiscalYearHelper.GetSelectedYear(YearComboBox);
                var sql = new StringBuilder(@"SELECT Id, VoucherNo, DonorName, Date, Amount, Quantity, DonationType, ProjectName
                    FROM Aids WHERE ProjectName = @ProjectName AND Year = @Year");
                var parameters = new Dictionary<string, object>
                {
                    { "@ProjectName", projectName },
                    { "@Year", selectedYear }
                };

                AppendVoucherAndDateFilters(sql, parameters, voucherFilter, fromDate, toDate);
                sql.Append(" ORDER BY date(Date) DESC, Id DESC");

                using (var cmd = new SqliteCommand(sql.ToString(), conn))
                {
                    foreach (var parameter in parameters) cmd.Parameters.AddWithValue(parameter.Key, parameter.Value);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new AidEntry
                            {
                                Id = reader.GetInt32(0),
                                VoucherNo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                DonorName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Date = ParseStoredDate(reader.GetString(3)),
                                Amount = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetValue(4)),
                                Quantity = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5)),
                                DonationType = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                                ProjectName = reader.GetString(7),
                                Year = selectedYear
                            });
                        }
                    }
                }
            }
            return list;
        }

        private List<AidEntry> LoadAllAidDetailsForPrinting(string projectFilter, string voucherFilter, DateTime? fromDate, DateTime? toDate)
        {
            var list = new List<AidEntry>();
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                int selectedYear = FiscalYearHelper.GetSelectedYear(YearComboBox);
                var sql = new StringBuilder(@"SELECT ProjectName, VoucherNo, DonorName, Date, Amount, Quantity, DonationType
                    FROM Aids WHERE Year = @Year");
                var parameters = new Dictionary<string, object> { { "@Year", selectedYear } };

                AppendAidFilters(sql, parameters, projectFilter, voucherFilter, fromDate, toDate);
                sql.Append(" ORDER BY ProjectName, date(Date), Id");

                using (var cmd = new SqliteCommand(sql.ToString(), conn))
                {
                    foreach (var parameter in parameters) cmd.Parameters.AddWithValue(parameter.Key, parameter.Value);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new AidEntry
                            {
                                ProjectName = reader.GetString(0),
                                VoucherNo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                DonorName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Date = ParseStoredDate(reader.GetString(3)),
                                Amount = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetValue(4)),
                                Quantity = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5)),
                                DonationType = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                                Year = selectedYear
                            });
                        }
                    }
                }
            }
            return list;
        }

        private static void AppendAidFilters(
            StringBuilder sql,
            Dictionary<string, object> parameters,
            string projectFilter,
            string voucherFilter,
            DateTime? fromDate,
            DateTime? toDate)
        {
            if (!string.IsNullOrWhiteSpace(projectFilter) && projectFilter != "جميع المشاريع")
            {
                sql.Append(" AND ProjectName = @ProjectName");
                parameters["@ProjectName"] = projectFilter;
            }

            AppendVoucherAndDateFilters(sql, parameters, voucherFilter, fromDate, toDate);
        }

        private static void AppendVoucherAndDateFilters(
            StringBuilder sql,
            Dictionary<string, object> parameters,
            string voucherFilter,
            DateTime? fromDate,
            DateTime? toDate)
        {
            if (!string.IsNullOrWhiteSpace(voucherFilter))
            {
                sql.Append(" AND VoucherNo LIKE @VoucherNo");
                parameters["@VoucherNo"] = "%" + voucherFilter + "%";
            }
            if (fromDate.HasValue)
            {
                sql.Append(" AND date(Date) >= date(@FromDate)");
                parameters["@FromDate"] = fromDate.Value.ToString("yyyy-MM-dd");
            }
            if (toDate.HasValue)
            {
                sql.Append(" AND date(Date) <= date(@ToDate)");
                parameters["@ToDate"] = toDate.Value.ToString("yyyy-MM-dd");
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

        #region UI & Helpers
        private string GetVoucherSearchText()
        {
            return _searchVoucherNoTextBox?.Text?.Trim();
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

        private bool TryGetSearchDateRange(bool showMessage, out DateTime? fromDate, out DateTime? toDate)
        {
            fromDate = null;
            toDate = null;

            if (!TryParseOptionalSearchDate(SearchFromDateTextBox.Text, "تاريخ بداية البحث", showMessage, out fromDate)) return false;
            if (!TryParseOptionalSearchDate(SearchToDateTextBox.Text, "تاريخ نهاية البحث", showMessage, out toDate)) return false;

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            {
                if (showMessage)
                {
                    MessageBox.Show(
                        "تاريخ بداية البحث يجب أن يكون قبل أو مساوياً لتاريخ النهاية.",
                        "نطاق تاريخ غير صحيح",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                return false;
            }

            return true;
        }

        private bool TryParseOptionalSearchDate(string text, string fieldName, bool showMessage, out DateTime? date)
        {
            date = null;
            if (string.IsNullOrWhiteSpace(text)) return true;

            if (!TryParseDate(text, out DateTime parsed, false))
            {
                if (showMessage)
                {
                    MessageBox.Show($"{fieldName} غير صحيح.", "خطأ في التاريخ", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return false;
            }

            if (parsed.Year != FiscalYearHelper.GetSelectedYear(YearComboBox))
            {
                if (showMessage)
                {
                    FiscalYearHelper.ValidateDateInSelectedYear(parsed, YearComboBox, fieldName);
                }
                return false;
            }

            date = parsed;
            return true;
        }

        private void ResetDateFieldsToSelectedYear()
        {
            FiscalYearHelper.ResetDateRange(YearComboBox, SearchFromDateTextBox, SearchToDateTextBox);
        }

        private bool PopulateAidEntry(AidEntry entry, string projectName)
        {
            string voucherNo = _voucherNoTextBox?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(voucherNo))
            {
                MessageBox.Show("الرجاء إدخال رقم السند.", "رقم السند مطلوب", MessageBoxButton.OK, MessageBoxImage.Warning);
                _voucherNoTextBox?.Focus();
                return false;
            }

            var fields = GetAidFields(projectName);
            if (fields == null || fields.Date == null || fields.Quantity == null || fields.Amount == null)
            {
                MessageBox.Show("لم يتم العثور على حقول الإدخال لهذا المشروع.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!TryParseDate(fields.Date.Text, out DateTime parsedDate, true)) return false;
            if (!TryParseNonNegativeInt(fields.Quantity.Text, "الكمية/العدد", out int quantity)) return false;
            if (!TryParseNonNegativeDecimal(fields.Amount.Text, "المبلغ", out decimal amount)) return false;

            if (quantity == 0 && amount == 0)
            {
                MessageBox.Show(
                    "يجب إدخال كمية/عدد أكبر من صفر أو مبلغ أكبر من صفر على الأقل.",
                    "بيانات المساعدة غير مكتملة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            string typeText = fields.Type?.Text?.Trim();
            if ((projectName == "إفطار صائم" || projectName == "الحقيبة المدرسية") && !string.IsNullOrWhiteSpace(typeText))
            {
                if (!int.TryParse(typeText, NumberStyles.Integer, CultureInfo.CurrentCulture, out int families) || families <= 0)
                {
                    MessageBox.Show("عدد الأسر يجب أن يكون رقماً صحيحاً أكبر من صفر.", "قيمة غير صحيحة", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                typeText = $"أسر: {families}";
            }

            entry.VoucherNo = voucherNo;
            entry.Date = parsedDate;
            entry.DonorName = fields.Donor?.Text?.Trim();
            entry.Quantity = quantity;
            entry.Amount = amount;
            entry.DonationType = typeText;
            entry.Year = FiscalYearHelper.GetSelectedYear(YearComboBox);
            return true;
        }

        private static bool TryParseNonNegativeInt(string text, string fieldName, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return true;

            if (!int.TryParse(text.Trim(), NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value) || value < 0)
            {
                MessageBox.Show($"{fieldName} يجب أن يكون رقماً صحيحاً صفراً أو أكبر.", "قيمة غير صحيحة", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private static bool TryParseNonNegativeDecimal(string text, string fieldName, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return true;

            string normalized = text.Trim();
            bool parsed = decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.CurrentCulture, out value) ||
                          decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value);

            if (!parsed || value < 0)
            {
                MessageBox.Show($"{fieldName} يجب أن يكون رقماً صفراً أو أكبر.", "قيمة غير صحيحة", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private void PopulateUIForEdit(AidEntry entry)
        {
            if (_voucherNoTextBox != null) _voucherNoTextBox.Text = entry.VoucherNo ?? string.Empty;

            var fields = GetAidFields(entry.ProjectName);
            if (fields == null) return;

            if (fields.Date != null) fields.Date.Text = entry.Date.ToString(AppSettings.DateFormat);
            if (fields.Donor != null) fields.Donor.Text = entry.DonorName;
            if (fields.Quantity != null) fields.Quantity.Text = entry.Quantity == 0 ? string.Empty : entry.Quantity.ToString();
            if (fields.Amount != null) fields.Amount.Text = entry.Amount == 0 ? string.Empty : entry.Amount.ToString("0.###");

            if (fields.Type != null)
            {
                if (!string.IsNullOrWhiteSpace(entry.DonationType) &&
                    (entry.ProjectName == "إفطار صائم" || entry.ProjectName == "الحقيبة المدرسية"))
                {
                    fields.Type.Text = entry.DonationType.Replace("أسر: ", string.Empty).Trim();
                }
                else
                {
                    fields.Type.Text = entry.DonationType;
                }
            }
        }

        private void RefreshSummaryGrid(bool showDateErrors)
        {
            if (YearComboBox.SelectedItem == null) return;
            if (!TryGetSearchDateRange(showDateErrors, out DateTime? fromDate, out DateTime? toDate)) return;

            string filterProject = SearchProjectComboBox.SelectedItem as string;
            string selectedProjectName = (ProjectsSummaryDataGrid.SelectedItem as ProjectSummary)?.ProjectName;

            var projects = LoadProjectsSummary(filterProject, GetVoucherSearchText(), fromDate, toDate);
            ProjectsSummaryDataGrid.ItemsSource = null;
            ProjectsSummaryDataGrid.ItemsSource = projects;

            if (!string.IsNullOrEmpty(selectedProjectName))
            {
                var itemToReselect = projects.FirstOrDefault(p => p.ProjectName == selectedProjectName);
                if (itemToReselect != null) ProjectsSummaryDataGrid.SelectedItem = itemToReselect;
            }

            if (ProjectsSummaryDataGrid.SelectedItem == null && projects.Count > 0)
            {
                ProjectsSummaryDataGrid.SelectedIndex = 0;
            }
            else if (projects.Count == 0)
            {
                AidsDataGrid.ItemsSource = null;
                UpdateDetailsColumns(null);
            }
        }

        private void UpdateDetailsColumns(string projectName)
        {
            QuantityColumn.Visibility = Visibility.Visible;
            DonationTypeColumn.Visibility = Visibility.Visible;

            switch (projectName)
            {
                case "الطرود الغذائية":
                    QuantityColumn.Header = "عدد الطرود";
                    DonationTypeColumn.Header = "نوع التبرع";
                    break;
                case "الملابس والأحذية":
                    QuantityColumn.Header = "عدد الأسر";
                    DonationTypeColumn.Header = string.Empty;
                    DonationTypeColumn.Visibility = Visibility.Collapsed;
                    break;
                case "معونة الشتاء":
                    QuantityColumn.Header = "عدد الأسر";
                    DonationTypeColumn.Header = "نوع التبرع";
                    break;
                case "الحقيبة المدرسية":
                    QuantityColumn.Header = "عدد الحقائب";
                    DonationTypeColumn.Header = "عدد الأسر";
                    break;
                case "كسوة العيد":
                case "الأضاحي":
                    QuantityColumn.Header = "عدد الأسر";
                    DonationTypeColumn.Header = string.Empty;
                    DonationTypeColumn.Visibility = Visibility.Collapsed;
                    break;
                case "إفطار صائم":
                    QuantityColumn.Header = "عدد الوجبات";
                    DonationTypeColumn.Header = "عدد الأسر";
                    break;
                case "أثاث منازل للفقراء":
                    QuantityColumn.Header = "عدد القطع";
                    DonationTypeColumn.Header = "البيان";
                    break;
                case "مواد مستهلكة للفقراء":
                case "مواد مستهلكة للمركز":
                case "أدوية ومستلزمات طبية":
                    QuantityColumn.Header = "الكمية";
                    DonationTypeColumn.Header = "البيان";
                    break;
                case "نذور وكفارات":
                case "أصول ثابتة":
                    QuantityColumn.Header = "العدد";
                    DonationTypeColumn.Header = "البيان";
                    break;
                default:
                    QuantityColumn.Header = "الكمية/العدد";
                    DonationTypeColumn.Header = "نوع التبرع";
                    break;
            }
        }

        private void ClearProjectFields(string projectName)
        {
            var fields = GetAidFields(projectName);
            fields?.Date?.Clear();
            fields?.Donor?.Clear();
            fields?.Type?.Clear();
            fields?.Quantity?.Clear();
            fields?.Amount?.Clear();
        }

        private void CancelCurrentEditAndClear()
        {
            string editProject = _aidToEdit?.ProjectName;
            ExitEditMode();
            if (!string.IsNullOrWhiteSpace(editProject)) ClearProjectFields(editProject);
            _voucherNoTextBox?.Clear();
        }

        private void SetButtonContent(string projectName, string content)
        {
            var fields = GetAidFields(projectName);
            if (fields?.SubmitButton != null) fields.SubmitButton.Content = content;
        }

        private AidFieldSet GetAidFields(string projectName)
        {
            switch (projectName)
            {
                case "الطرود الغذائية":
                    return new AidFieldSet { Date = FoodDateTextBox, Donor = FoodDonorTextBox, Type = FoodTypeTextBox, Quantity = FoodQuantityTextBox, Amount = FoodAmountTextBox, SubmitButton = AddFoodButton };
                case "الملابس والأحذية":
                    return new AidFieldSet { Date = ClothesDateTextBox, Donor = ClothesDonor, Quantity = ClothesQuantity, Amount = ClothesAmount, SubmitButton = AddClothesButton };
                case "معونة الشتاء":
                    return new AidFieldSet { Date = WinterDateTextBox, Donor = WinterDonor, Type = WinterType, Quantity = WinterQuantity, Amount = WinterAmount, SubmitButton = AddWinterButton };
                case "الحقيبة المدرسية":
                    return new AidFieldSet { Date = BagDateTextBox, Donor = BagDonor, Type = BagFamilies, Quantity = BagQuantity, Amount = BagAmount, SubmitButton = AddBagButton };
                case "كسوة العيد":
                    return new AidFieldSet { Date = EidDateTextBox, Donor = EidDonor, Quantity = EidQuantity, Amount = EidAmount, SubmitButton = AddEidButton };
                case "إفطار صائم":
                    return new AidFieldSet { Date = IftarDateTextBox, Donor = IftarDonor, Type = IftarFamilies, Quantity = IftarQuantity, Amount = IftarAmount, SubmitButton = AddIftarButton };
                case "الأضاحي":
                    return new AidFieldSet { Date = UdhiyahDateTextBox, Donor = UdhiyahDonor, Quantity = UdhiyahQuantity, Amount = UdhiyahAmount, SubmitButton = AddUdhiyahButton };
                case "أثاث منازل للفقراء":
                    return new AidFieldSet { Date = FurnitureDateTextBox, Donor = FurnitureDonor, Type = FurnitureType, Quantity = FurnitureQuantity, Amount = FurnitureAmount, SubmitButton = AddFurnitureButton };
                case "مواد مستهلكة للفقراء":
                    return new AidFieldSet { Date = ConsumablesPoorDateTextBox, Donor = ConsumablesPoorDonor, Type = ConsumablesPoorType, Quantity = ConsumablesPoorQuantity, Amount = ConsumablesPoorAmount, SubmitButton = AddConsumablesPoorButton };
                case "نذور وكفارات":
                    return new AidFieldSet { Date = VowsDateTextBox, Donor = VowsDonor, Type = VowsType, Quantity = VowsQuantity, Amount = VowsAmount, SubmitButton = AddVowsButton };
                case "أصول ثابتة":
                    return new AidFieldSet { Date = FixedAssetsDateTextBox, Donor = FixedAssetsDonor, Type = FixedAssetsType, Quantity = FixedAssetsQuantity, Amount = FixedAssetsAmount, SubmitButton = AddFixedAssetsButton };
                case "مواد مستهلكة للمركز":
                    return new AidFieldSet { Date = ConsumablesCenterDateTextBox, Donor = ConsumablesCenterDonor, Type = ConsumablesCenterType, Quantity = ConsumablesCenterQuantity, Amount = ConsumablesCenterAmount, SubmitButton = AddConsumablesCenterButton };
                case "أدوية ومستلزمات طبية":
                    return new AidFieldSet { Date = MedicalDateTextBox, Donor = MedicalDonor, Type = MedicalType, Quantity = MedicalQuantity, Amount = MedicalAmount, SubmitButton = AddMedicalButton };
                default:
                    return null;
            }
        }

        private void ExitEditMode()
        {
            if (_aidToEdit != null)
            {
                SetButtonContent(_aidToEdit.ProjectName, "إضافة إدخال");
            }
            SetButtonContent((AidEntryTabControl.SelectedItem as TabItem)?.Header?.ToString(), "إضافة إدخال");
            _aidToEdit = null;
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

        private void LastField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddAid_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private FlowDocument CreatePrintDocument(List<AidEntry> data, string project, DateTime? fromDate, DateTime? toDate)
        {
            var doc = new FlowDocument
            {
                FlowDirection = FlowDirection.RightToLeft,
                FontFamily = new FontFamily("Arial")
            };

            string title = "تقرير المساعدات" + (!string.IsNullOrEmpty(project) ? $" - {project}" : string.Empty);
            doc.Blocks.Add(new Paragraph(new Run(title))
            {
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center
            });

            string voucherFilter = GetVoucherSearchText();
            string voucherPart = string.IsNullOrWhiteSpace(voucherFilter) ? string.Empty : $"    رقم السند: {voucherFilter}";
            doc.Blocks.Add(new Paragraph(new Run(
                $"السنة المالية: {YearComboBox.SelectedItem}    من: {(fromDate.HasValue ? fromDate.Value.ToString(AppSettings.DateFormat) : "البداية")}  إلى: {(toDate.HasValue ? toDate.Value.ToString(AppSettings.DateFormat) : "النهاية")}{voucherPart}"))
            {
                FontSize = 12,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            });

            var table = new Table
            {
                CellSpacing = 0,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };
            doc.Blocks.Add(table);

            table.Columns.Add(new TableColumn { Width = new GridLength(1.3, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(0.8, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1.3, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(0.9, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(0.8, GridUnitType.Star) });

            var headerGroup = new TableRowGroup();
            table.RowGroups.Add(headerGroup);
            var headerRow = new TableRow { Background = Brushes.LightGray, FontWeight = FontWeights.Bold };
            headerGroup.Rows.Add(headerRow);
            headerRow.Cells.Add(CreatePrintCell("المشروع", true));
            headerRow.Cells.Add(CreatePrintCell("رقم السند", true));
            headerRow.Cells.Add(CreatePrintCell("المتبرع", true));
            headerRow.Cells.Add(CreatePrintCell("التاريخ", true));
            headerRow.Cells.Add(CreatePrintCell("الكمية/النوع", true));
            headerRow.Cells.Add(CreatePrintCell("المبلغ", true));

            var dataGroup = new TableRowGroup();
            table.RowGroups.Add(dataGroup);
            foreach (var item in data)
            {
                var row = new TableRow();
                dataGroup.Rows.Add(row);
                row.Cells.Add(CreatePrintCell(item.ProjectName));
                row.Cells.Add(CreatePrintCell(item.VoucherNo));
                row.Cells.Add(CreatePrintCell(item.DonorName));
                row.Cells.Add(CreatePrintCell(item.Date.ToString(AppSettings.DateFormat)));

                string quantityText = item.Quantity > 0 ? item.Quantity.ToString() : string.Empty;
                if (!string.IsNullOrWhiteSpace(item.DonationType))
                {
                    quantityText += string.IsNullOrEmpty(quantityText)
                        ? item.DonationType
                        : $" ({item.DonationType})";
                }

                row.Cells.Add(CreatePrintCell(quantityText));
                row.Cells.Add(CreatePrintCell(item.Amount > 0 ? item.Amount.ToString("N3") : string.Empty));
            }

            return doc;
        }

        private static TableCell CreatePrintCell(string text, bool header = false)
        {
            var paragraph = new Paragraph(new Run(text ?? string.Empty)) { Margin = new Thickness(3) };
            if (header) paragraph.FontWeight = FontWeights.Bold;
            return new TableCell(paragraph)
            {
                Padding = new Thickness(5),
                BorderBrush = header ? Brushes.Gray : Brushes.Gainsboro,
                BorderThickness = new Thickness(0, 0, 1, 1)
            };
        }

        private void AddUpdate_Click(object sender, RoutedEventArgs e)
        {
            AddAid_Click(sender, e);
        }
        #endregion
    }
}
