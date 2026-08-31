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
using System.Windows.Threading;

namespace AccountingApp
{
    public partial class AidsView : UserControl
    {
        private readonly string _connectionString = DatabaseService.ConnectionString;
        private readonly DispatcherTimer _searchTimer;
        private AidEntry _aidToEdit;
        private bool _suppressEvents;

        private readonly List<string> _projectNames = new List<string>
        {
            "الطرود الغذائية",
            "الملابس والأحذية",
            "معونة الشتاء",
            "الحقيبة المدرسية",
            "كسوة العيد",
            "إفطار صائم",
            "الأضاحي",
            "أثاث منازل للفقراء",
            "مواد مستهلكة للفقراء",
            "نذور وكفارات",
            "أصول ثابتة",
            "مواد مستهلكة للمركز",
            "أدوية ومستلزمات طبية"
        };

        private sealed class AidProjectConfig
        {
            public string Name { get; set; }
            public string QuantityLabel { get; set; }
            public string TypeLabel { get; set; }
            public bool ShowType { get; set; }
            public bool TypeMustBePositiveInteger { get; set; }
            public string Hint { get; set; }
        }

        private readonly Dictionary<string, AidProjectConfig> _projectConfigs;

        public AidsView()
        {
            InitializeComponent();
            DatabaseService.InitializeDatabase();

            _projectConfigs = BuildProjectConfigs();
            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
            _searchTimer.Tick += SearchTimer_Tick;

            PopulateComboBoxes();
        }

        private Dictionary<string, AidProjectConfig> BuildProjectConfigs()
        {
            var configs = new List<AidProjectConfig>
            {
                NewConfig("الطرود الغذائية", "عدد الطرود", "نوع التبرع", true, false, "أدخل نوع التبرع وعدد الطرود والمبلغ إن وجد."),
                NewConfig("الملابس والأحذية", "عدد الأسر", null, false, false, "أدخل عدد الأسر والمبلغ إن وجد."),
                NewConfig("معونة الشتاء", "عدد الأسر", "نوع التبرع", true, false, "مثال: بطانيات، مدافئ، وقود أو ملابس شتوية."),
                NewConfig("الحقيبة المدرسية", "عدد الحقائب", "عدد الأسر", true, true, "أدخل عدد الحقائب وعدد الأسر المستفيدة."),
                NewConfig("كسوة العيد", "عدد الأسر", null, false, false, "أدخل عدد الأسر المستفيدة والمبلغ إن وجد."),
                NewConfig("إفطار صائم", "عدد الوجبات", "عدد الأسر", true, true, "أدخل عدد الوجبات وعدد الأسر المستفيدة."),
                NewConfig("الأضاحي", "عدد الأسر", null, false, false, "أدخل عدد الأسر المستفيدة والمبلغ إن وجد."),
                NewConfig("أثاث منازل للفقراء", "عدد القطع", "البيان", true, false, "في البيان اكتب نوع الأثاث أو وصفه."),
                NewConfig("مواد مستهلكة للفقراء", "الكمية", "البيان", true, false, "في البيان اكتب نوع المواد المصروفة."),
                NewConfig("نذور وكفارات", "العدد", "البيان", true, false, "في البيان اكتب وصف النذر أو الكفارة عند الحاجة."),
                NewConfig("أصول ثابتة", "العدد", "البيان", true, false, "في البيان اكتب وصف الأصل الثابت."),
                NewConfig("مواد مستهلكة للمركز", "الكمية", "البيان", true, false, "في البيان اكتب نوع المواد المستخدمة للمركز."),
                NewConfig("أدوية ومستلزمات طبية", "الكمية", "البيان", true, false, "في البيان اكتب اسم الدواء أو المستلزم الطبي.")
            };

            return configs.ToDictionary(c => c.Name, StringComparer.Ordinal);
        }

        private static AidProjectConfig NewConfig(
            string name,
            string quantityLabel,
            string typeLabel,
            bool showType,
            bool typeMustBePositiveInteger,
            string hint)
        {
            return new AidProjectConfig
            {
                Name = name,
                QuantityLabel = quantityLabel,
                TypeLabel = typeLabel,
                ShowType = showType,
                TypeMustBePositiveInteger = typeMustBePositiveInteger,
                Hint = hint
            };
        }

        private void PopulateComboBoxes()
        {
            _suppressEvents = true;
            try
            {
                FiscalYearHelper.SelectCurrentYear(YearComboBox);

                EntryProjectComboBox.ItemsSource = _projectNames;
                EntryProjectComboBox.SelectedIndex = 0;

                var searchProjects = new List<string> { "جميع المشاريع" };
                searchProjects.AddRange(_projectNames);
                SearchProjectComboBox.ItemsSource = searchProjects;
                SearchProjectComboBox.SelectedIndex = 0;
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        private void AidsView_Loaded(object sender, RoutedEventArgs e)
        {
            _suppressEvents = true;
            try
            {
                FiscalYearHelper.SelectCurrentYear(YearComboBox);
                if (EntryProjectComboBox.SelectedItem == null) EntryProjectComboBox.SelectedIndex = 0;
                if (SearchProjectComboBox.SelectedItem == null) SearchProjectComboBox.SelectedIndex = 0;
                ResetSearchDateRange();
                SetDefaultEntryDate();
            }
            finally
            {
                _suppressEvents = false;
            }

            ApplyProjectConfiguration();
            RefreshAll(false);
            VoucherNoTextBox.Focus();
        }

        private void YearComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents || !IsLoaded || YearComboBox.SelectedItem == null) return;

            ExitEditMode();
            ClearEntryFields(false);
            ResetSearchDateRange();
            SetDefaultEntryDate();
            RefreshAll(false);
            SetEntryStatus("تم تغيير السنة المالية. كل النتائج والإدخالات الآن تخص السنة المختارة.", false);
        }

        private void EntryProjectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents || EntryProjectComboBox.SelectedItem == null) return;

            if (_aidToEdit != null &&
                !string.Equals(_aidToEdit.ProjectName, EntryProjectComboBox.SelectedItem.ToString(), StringComparison.Ordinal))
            {
                ExitEditMode();
                ClearEntryFields(true);
                SetEntryStatus("تم إلغاء وضع التعديل لأنك غيرت نوع المساعدة.", false);
            }

            ApplyProjectConfiguration();
            RefreshRecentEntries();
            if (IsLoaded) VoucherNoTextBox.Focus();
        }

        private void SearchProjectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents || !IsLoaded) return;
            UpdateSearchResultColumns();
            RefreshSearchResults(false);
        }

        private void QuickSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressEvents || !IsLoaded) return;
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void SearchTimer_Tick(object sender, EventArgs e)
        {
            _searchTimer.Stop();
            RefreshSearchResults(false);
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            RefreshSearchResults(true);
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            _suppressEvents = true;
            try
            {
                QuickSearchTextBox.Clear();
                SearchProjectComboBox.SelectedIndex = 0;
                ResetSearchDateRange();
            }
            finally
            {
                _suppressEvents = false;
            }

            UpdateSearchResultColumns();
            RefreshSearchResults(false);
            QuickSearchTextBox.Focus();
        }

        private void SaveAid_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentEntry();
        }

        private void SaveCurrentEntry()
        {
            AidEntry entry;
            if (!TryBuildEntry(out entry)) return;
            if (!FiscalYearHelper.ValidateDateInSelectedYear(entry.Date, YearComboBox, "تاريخ المساعدة")) return;

            int ignoredId = _aidToEdit == null ? 0 : _aidToEdit.Id;
            AidEntry duplicate = FindDuplicateVoucher(entry.VoucherNo, entry.Year, ignoredId);
            if (duplicate != null)
            {
                string duplicateDate = duplicate.Date.ToString(AppSettings.DateFormat);
                string message =
                    $"رقم السند {entry.VoucherNo} موجود مسبقًا في نفس السنة المالية.\n\n" +
                    $"المساعدة: {duplicate.ProjectName}\n" +
                    $"التاريخ: {duplicateDate}\n" +
                    $"المتبرع: {duplicate.DonorName}\n\n" +
                    "هل تريد حفظ هذا السجل رغم التكرار؟";

                if (MessageBox.Show(message, "تنبيه: رقم سند مكرر", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    VoucherNoTextBox.Focus();
                    VoucherNoTextBox.SelectAll();
                    return;
                }
            }

            bool isEdit = _aidToEdit != null;
            bool saved;
            if (isEdit)
            {
                entry.Id = _aidToEdit.Id;
                saved = UpdateAidEntry(entry);
            }
            else
            {
                saved = SaveAidEntry(entry);
            }

            if (!saved) return;

            string savedVoucher = entry.VoucherNo;
            string savedProject = entry.ProjectName;
            string savedDate = AidDateTextBox.Text;

            ExitEditMode();
            ClearEntryFields(true);
            AidDateTextBox.Text = savedDate;

            SetEntryStatus(
                isEdit
                    ? $"تم تحديث السند {savedVoucher} بنجاح."
                    : $"تم حفظ السند {savedVoucher} بنجاح. يمكنك إدخال سند جديد لنفس نوع المساعدة.",
                true);

            RefreshSearchResults(false);
            RefreshRecentEntries();

            if (!string.Equals(EntryProjectComboBox.SelectedItem?.ToString(), savedProject, StringComparison.Ordinal))
            {
                _suppressEvents = true;
                EntryProjectComboBox.SelectedItem = savedProject;
                _suppressEvents = false;
                ApplyProjectConfiguration();
            }

            VoucherNoTextBox.Focus();
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            ExitEditMode();
            ClearEntryFields(true);
            SetEntryStatus("تم إلغاء التعديل. النموذج جاهز لإضافة سجل جديد.", false);
            VoucherNoTextBox.Focus();
        }

        private void EditAid_Click(object sender, RoutedEventArgs e)
        {
            AidEntry entry = (sender as FrameworkElement)?.DataContext as AidEntry;
            if (entry != null) BeginEdit(entry);
        }

        private void AidDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var grid = sender as DataGrid;
            AidEntry entry = grid?.SelectedItem as AidEntry;
            if (entry != null) BeginEdit(entry);
        }

        private void BeginEdit(AidEntry entry)
        {
            _aidToEdit = entry;

            _suppressEvents = true;
            try
            {
                EntryProjectComboBox.SelectedItem = entry.ProjectName;
            }
            finally
            {
                _suppressEvents = false;
            }

            ApplyProjectConfiguration();

            VoucherNoTextBox.Text = entry.VoucherNo ?? string.Empty;
            AidDateTextBox.Text = entry.Date.ToString(AppSettings.DateFormat);
            DonorNameTextBox.Text = entry.DonorName ?? string.Empty;
            QuantityTextBox.Text = entry.Quantity == 0 ? string.Empty : entry.Quantity.ToString(CultureInfo.CurrentCulture);
            AmountTextBox.Text = entry.Amount == 0 ? string.Empty : entry.Amount.ToString("0.###", CultureInfo.CurrentCulture);

            AidProjectConfig config = GetSelectedEntryConfig();
            if (config != null && config.ShowType)
            {
                string typeText = entry.DonationType ?? string.Empty;
                if (config.TypeMustBePositiveInteger && typeText.StartsWith("أسر: ", StringComparison.Ordinal))
                {
                    typeText = typeText.Substring("أسر: ".Length).Trim();
                }
                DonationTypeTextBox.Text = typeText;
            }
            else
            {
                DonationTypeTextBox.Clear();
            }

            EntryModeTextBlock.Text = $"تعديل مساعدة - السند {entry.VoucherNo}";
            SaveAidButton.Content = "حفظ التعديل";
            CancelEditButton.Visibility = Visibility.Visible;
            SetEntryStatus("تم تحميل السجل للتعديل. غيّر المطلوب ثم اضغط حفظ التعديل.", false);
            VoucherNoTextBox.Focus();
            VoucherNoTextBox.SelectAll();
        }

        private void DeleteAid_Click(object sender, RoutedEventArgs e)
        {
            AidEntry entry = (sender as FrameworkElement)?.DataContext as AidEntry;
            if (entry == null) return;

            string message =
                $"هل تريد حذف هذا السجل؟\n\n" +
                $"رقم السند: {entry.VoucherNo}\n" +
                $"نوع المساعدة: {entry.ProjectName}\n" +
                $"التاريخ: {entry.Date.ToString(AppSettings.DateFormat)}\n" +
                $"المتبرع: {entry.DonorName}";

            if (MessageBox.Show(message, "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            if (!DeleteAidEntry(entry.Id)) return;

            if (_aidToEdit != null && _aidToEdit.Id == entry.Id)
            {
                ExitEditMode();
                ClearEntryFields(true);
            }

            SetEntryStatus($"تم حذف السند {entry.VoucherNo}.", false);
            RefreshSearchResults(false);
            RefreshRecentEntries();
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            if (!RefreshSearchResults(true)) return;

            var data = (AidsDataGrid.ItemsSource as IEnumerable<AidEntry>)?.ToList() ?? new List<AidEntry>();
            if (data.Count == 0)
            {
                MessageBox.Show("لا توجد نتائج للطباعة.", "طباعة", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true) return;

            DateTime? fromDate;
            DateTime? toDate;
            TryGetSearchDateRange(false, out fromDate, out toDate);

            FlowDocument document = CreatePrintDocument(data, fromDate, toDate);
            document.PagePadding = new Thickness(35);
            document.ColumnWidth = dialog.PrintableAreaWidth;
            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "تقرير المساعدات");
        }

        private void ApplyProjectConfiguration()
        {
            AidProjectConfig config = GetSelectedEntryConfig();
            if (config == null) return;

            QuantityLabelTextBlock.Text = config.QuantityLabel + ":";
            DonationTypeLabelTextBlock.Text = (config.TypeLabel ?? "البيان") + ":";
            DonationTypePanel.Visibility = config.ShowType ? Visibility.Visible : Visibility.Collapsed;
            EntryProjectHintTextBlock.Text = config.Hint;
            RecentTitleTextBlock.Text = "آخر 10 إدخالات - " + config.Name;

            RecentQuantityColumn.Header = config.QuantityLabel;
            RecentDonationTypeColumn.Header = config.TypeLabel ?? "البيان";
            RecentDonationTypeColumn.Visibility = config.ShowType ? Visibility.Visible : Visibility.Collapsed;

            if (!config.ShowType && _aidToEdit == null)
            {
                DonationTypeTextBox.Clear();
            }
        }

        private AidProjectConfig GetSelectedEntryConfig()
        {
            string project = EntryProjectComboBox.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(project)) return null;

            AidProjectConfig config;
            return _projectConfigs.TryGetValue(project, out config) ? config : null;
        }

        private void UpdateSearchResultColumns()
        {
            string project = SearchProjectComboBox.SelectedItem?.ToString();
            AidProjectConfig config;
            if (!string.IsNullOrWhiteSpace(project) &&
                !string.Equals(project, "جميع المشاريع", StringComparison.Ordinal) &&
                _projectConfigs.TryGetValue(project, out config))
            {
                QuantityColumn.Header = config.QuantityLabel;
                DonationTypeColumn.Header = config.TypeLabel ?? "البيان";
                DonationTypeColumn.Visibility = config.ShowType ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                QuantityColumn.Header = "الكمية/العدد";
                DonationTypeColumn.Header = "البيان/النوع";
                DonationTypeColumn.Visibility = Visibility.Visible;
            }
        }

        private void RefreshAll(bool showDateErrors)
        {
            UpdateSearchResultColumns();
            RefreshSearchResults(showDateErrors);
            RefreshRecentEntries();
        }

        private bool RefreshSearchResults(bool showDateErrors)
        {
            if (YearComboBox.SelectedItem == null) return false;

            DateTime? fromDate;
            DateTime? toDate;
            if (!TryGetSearchDateRange(showDateErrors, out fromDate, out toDate)) return false;

            string project = SearchProjectComboBox.SelectedItem?.ToString();
            if (string.Equals(project, "جميع المشاريع", StringComparison.Ordinal)) project = null;

            string quick = QuickSearchTextBox.Text?.Trim();
            int year = FiscalYearHelper.GetSelectedYear(YearComboBox);
            List<AidEntry> entries = LoadFilteredEntries(year, project, quick, fromDate, toDate);

            AidsDataGrid.ItemsSource = entries;

            decimal totalAmount = entries.Sum(x => x.Amount);
            int totalQuantity = entries.Sum(x => x.Quantity);
            SearchSummaryTextBlock.Text =
                $"عدد النتائج: {entries.Count:N0}    |    إجمالي المبلغ: {totalAmount:N3}    |    إجمالي الكمية/العدد: {totalQuantity:N0}";

            return true;
        }

        private void RefreshRecentEntries()
        {
            if (YearComboBox.SelectedItem == null || EntryProjectComboBox.SelectedItem == null) return;

            int year = FiscalYearHelper.GetSelectedYear(YearComboBox);
            string project = EntryProjectComboBox.SelectedItem.ToString();
            RecentEntriesDataGrid.ItemsSource = LoadRecentEntries(year, project, 10);
        }

        private bool TryBuildEntry(out AidEntry entry)
        {
            entry = null;

            AidProjectConfig config = GetSelectedEntryConfig();
            if (config == null)
            {
                MessageBox.Show("الرجاء اختيار نوع المساعدة.", "نوع المساعدة", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            string voucherNo = VoucherNoTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(voucherNo))
            {
                MessageBox.Show("الرجاء إدخال رقم السند.", "رقم السند مطلوب", MessageBoxButton.OK, MessageBoxImage.Warning);
                VoucherNoTextBox.Focus();
                return false;
            }

            DateTime date;
            if (!TryParseEntryDate(AidDateTextBox.Text, out date, true))
            {
                AidDateTextBox.Focus();
                return false;
            }

            int quantity;
            if (!TryParseNonNegativeInt(QuantityTextBox.Text, config.QuantityLabel, out quantity))
            {
                QuantityTextBox.Focus();
                return false;
            }

            decimal amount;
            if (!TryParseNonNegativeDecimal(AmountTextBox.Text, "المبلغ", out amount))
            {
                AmountTextBox.Focus();
                return false;
            }

            if (quantity == 0 && amount == 0)
            {
                MessageBox.Show(
                    "أدخل كمية/عدد أكبر من صفر أو مبلغًا أكبر من صفر على الأقل.",
                    "بيانات غير مكتملة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                QuantityTextBox.Focus();
                return false;
            }

            string donationType = null;
            if (config.ShowType)
            {
                donationType = DonationTypeTextBox.Text?.Trim();
                if (config.TypeMustBePositiveInteger && !string.IsNullOrWhiteSpace(donationType))
                {
                    int families;
                    if (!int.TryParse(donationType, NumberStyles.Integer, CultureInfo.CurrentCulture, out families) || families <= 0)
                    {
                        MessageBox.Show(config.TypeLabel + " يجب أن يكون رقمًا صحيحًا أكبر من صفر.", "قيمة غير صحيحة", MessageBoxButton.OK, MessageBoxImage.Warning);
                        DonationTypeTextBox.Focus();
                        return false;
                    }
                    donationType = "أسر: " + families.ToString(CultureInfo.CurrentCulture);
                }
            }

            entry = new AidEntry
            {
                ProjectName = config.Name,
                VoucherNo = voucherNo,
                DonorName = DonorNameTextBox.Text?.Trim(),
                Date = date,
                Quantity = quantity,
                Amount = amount,
                DonationType = donationType,
                Year = FiscalYearHelper.GetSelectedYear(YearComboBox)
            };

            return true;
        }

        private AidEntry FindDuplicateVoucher(string voucherNo, int year, int excludeId)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqliteCommand(@"
SELECT Id, ProjectName, VoucherNo, DonorName, Date, Amount, Quantity, DonationType, Year
FROM Aids
WHERE Year = @Year
  AND VoucherNo = @VoucherNo COLLATE NOCASE
  AND Id <> @ExcludeId
ORDER BY Id DESC
LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("@Year", year);
                    cmd.Parameters.AddWithValue("@VoucherNo", voucherNo.Trim());
                    cmd.Parameters.AddWithValue("@ExcludeId", excludeId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        return reader.Read() ? ReadAidEntry(reader) : null;
                    }
                }
            }
        }

        private bool SaveAidEntry(AidEntry entry)
        {
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqliteCommand(@"
INSERT INTO Aids (ProjectName, VoucherNo, DonorName, Date, Amount, Quantity, DonationType, Year)
VALUES (@ProjectName, @VoucherNo, @DonorName, @Date, @Amount, @Quantity, @DonationType, @Year)", conn))
                    {
                        AddAidParameters(cmd, entry, false);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("تعذر حفظ بيانات المساعدة.\n" + ex.Message, "خطأ في الحفظ", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    using (var cmd = new SqliteCommand(@"
UPDATE Aids SET
    ProjectName = @ProjectName,
    VoucherNo = @VoucherNo,
    DonorName = @DonorName,
    Date = @Date,
    Amount = @Amount,
    Quantity = @Quantity,
    DonationType = @DonationType,
    Year = @Year
WHERE Id = @Id", conn))
                    {
                        AddAidParameters(cmd, entry, true);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("تعذر تحديث بيانات المساعدة.\n" + ex.Message, "خطأ في التحديث", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private static void AddAidParameters(SqliteCommand cmd, AidEntry entry, bool includeId)
        {
            cmd.Parameters.AddWithValue("@ProjectName", entry.ProjectName);
            cmd.Parameters.AddWithValue("@VoucherNo", entry.VoucherNo);
            cmd.Parameters.AddWithValue("@DonorName", (object)entry.DonorName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Date", entry.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
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
            catch (Exception ex)
            {
                MessageBox.Show("تعذر حذف السجل.\n" + ex.Message, "خطأ في الحذف", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private List<AidEntry> LoadRecentEntries(int year, string project, int limit)
        {
            var result = new List<AidEntry>();
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqliteCommand(@"
SELECT Id, ProjectName, VoucherNo, DonorName, Date, Amount, Quantity, DonationType, Year
FROM Aids
WHERE Year = @Year AND ProjectName = @ProjectName
ORDER BY date(Date) DESC, Id DESC
LIMIT @Limit", conn))
                {
                    cmd.Parameters.AddWithValue("@Year", year);
                    cmd.Parameters.AddWithValue("@ProjectName", project);
                    cmd.Parameters.AddWithValue("@Limit", limit);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read()) result.Add(ReadAidEntry(reader));
                    }
                }
            }
            return result;
        }

        private List<AidEntry> LoadFilteredEntries(
            int year,
            string project,
            string quickSearch,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var result = new List<AidEntry>();
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var sql = new StringBuilder(@"
SELECT Id, ProjectName, VoucherNo, DonorName, Date, Amount, Quantity, DonationType, Year
FROM Aids
WHERE Year = @Year");

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Parameters.AddWithValue("@Year", year);

                    if (!string.IsNullOrWhiteSpace(project))
                    {
                        sql.Append(" AND ProjectName = @ProjectName");
                        cmd.Parameters.AddWithValue("@ProjectName", project);
                    }

                    if (!string.IsNullOrWhiteSpace(quickSearch))
                    {
                        sql.Append(@" AND (
                            COALESCE(VoucherNo, '') LIKE @Quick OR
                            COALESCE(DonorName, '') LIKE @Quick OR
                            COALESCE(DonationType, '') LIKE @Quick OR
                            ProjectName LIKE @Quick
                        )");
                        cmd.Parameters.AddWithValue("@Quick", "%" + quickSearch.Trim() + "%");
                    }

                    if (fromDate.HasValue)
                    {
                        sql.Append(" AND date(Date) >= date(@FromDate)");
                        cmd.Parameters.AddWithValue("@FromDate", fromDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    }

                    if (toDate.HasValue)
                    {
                        sql.Append(" AND date(Date) <= date(@ToDate)");
                        cmd.Parameters.AddWithValue("@ToDate", toDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    }

                    sql.Append(" ORDER BY date(Date) DESC, Id DESC");
                    cmd.CommandText = sql.ToString();

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read()) result.Add(ReadAidEntry(reader));
                    }
                }
            }
            return result;
        }

        private static AidEntry ReadAidEntry(SqliteDataReader reader)
        {
            return new AidEntry
            {
                Id = reader.GetInt32(0),
                ProjectName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                VoucherNo = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                DonorName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Date = ParseStoredDate(reader.GetString(4)),
                Amount = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetValue(5)),
                Quantity = reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader.GetValue(6)),
                DonationType = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                Year = reader.IsDBNull(8) ? 0 : Convert.ToInt32(reader.GetValue(8))
            };
        }

        private static DateTime ParseStoredDate(string value)
        {
            DateTime date;
            if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return date;
            }
            return DateTime.Parse(value, CultureInfo.CurrentCulture);
        }

        private bool TryGetSearchDateRange(bool showMessage, out DateTime? fromDate, out DateTime? toDate)
        {
            fromDate = null;
            toDate = null;

            if (!TryParseOptionalSearchDate(SearchFromDateTextBox.Text, "تاريخ البداية", showMessage, out fromDate)) return false;
            if (!TryParseOptionalSearchDate(SearchToDateTextBox.Text, "تاريخ النهاية", showMessage, out toDate)) return false;

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            {
                if (showMessage)
                {
                    MessageBox.Show("تاريخ البداية يجب أن يكون قبل أو مساويًا لتاريخ النهاية.", "نطاق تاريخ غير صحيح", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return false;
            }

            return true;
        }

        private bool TryParseOptionalSearchDate(string text, string fieldName, bool showMessage, out DateTime? date)
        {
            date = null;
            if (string.IsNullOrWhiteSpace(text)) return true;

            DateTime parsed;
            if (!TryParseDateForSelectedYear(text, out parsed, false))
            {
                if (showMessage)
                {
                    MessageBox.Show(fieldName + " غير صحيح.", "خطأ في التاريخ", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return false;
            }

            int year = FiscalYearHelper.GetSelectedYear(YearComboBox);
            if (parsed.Year != year)
            {
                if (showMessage) FiscalYearHelper.ValidateDateInSelectedYear(parsed, YearComboBox, fieldName);
                return false;
            }

            date = parsed;
            return true;
        }

        private bool TryParseEntryDate(string text, out DateTime date, bool showMessage)
        {
            return TryParseDateForSelectedYear(text, out date, showMessage);
        }

        private bool TryParseDateForSelectedYear(string text, out DateTime date, bool showMessage)
        {
            int year = FiscalYearHelper.GetSelectedYear(YearComboBox);
            return DatabaseService.TryParseDate(text, out date, showMessage, year);
        }

        private void ResetSearchDateRange()
        {
            FiscalYearHelper.ResetDateRange(YearComboBox, SearchFromDateTextBox, SearchToDateTextBox);
        }

        private void SetDefaultEntryDate()
        {
            int year = FiscalYearHelper.GetSelectedYear(YearComboBox);
            DateTime date = year == DateTime.Now.Year ? DateTime.Now.Date : new DateTime(year, 1, 1);
            AidDateTextBox.Text = date.ToString(AppSettings.DateFormat);
        }

        private void ClearEntryFields(bool keepDate)
        {
            string date = keepDate ? AidDateTextBox.Text : null;
            VoucherNoTextBox.Clear();
            DonorNameTextBox.Clear();
            DonationTypeTextBox.Clear();
            QuantityTextBox.Clear();
            AmountTextBox.Clear();

            if (keepDate && !string.IsNullOrWhiteSpace(date))
            {
                AidDateTextBox.Text = date;
            }
            else
            {
                SetDefaultEntryDate();
            }
        }

        private void ExitEditMode()
        {
            _aidToEdit = null;
            EntryModeTextBlock.Text = "إضافة مساعدة جديدة";
            SaveAidButton.Content = "حفظ المساعدة";
            CancelEditButton.Visibility = Visibility.Collapsed;
        }

        private void SetEntryStatus(string message, bool success)
        {
            EntryStatusTextBlock.Text = message;
            EntryStatusTextBlock.Foreground = success
                ? new SolidColorBrush(Color.FromRgb(39, 103, 73))
                : new SolidColorBrush(Color.FromRgb(74, 85, 104));
        }

        private static bool TryParseNonNegativeInt(string text, string fieldName, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return true;

            if (!int.TryParse(text.Trim(), NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value) || value < 0)
            {
                MessageBox.Show(fieldName + " يجب أن يكون رقمًا صحيحًا صفرًا أو أكبر.", "قيمة غير صحيحة", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show(fieldName + " يجب أن يكون رقمًا صفرًا أو أكبر.", "قيمة غير صحيحة", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            var request = new TraversalRequest(FocusNavigationDirection.Next);
            UIElement focused = Keyboard.FocusedElement as UIElement;
            focused?.MoveFocus(request);
            e.Handled = true;
        }

        private void EntryDate_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            DateTime date;
            if (!TryParseEntryDate(AidDateTextBox.Text, out date, true))
            {
                e.Handled = true;
                return;
            }

            AidDateTextBox.Text = date.ToString(AppSettings.DateFormat);
            MoveFocusOnEnter(sender, e);
        }

        private void SearchDate_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            var textBox = sender as TextBox;
            if (textBox != null && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                DateTime date;
                if (!TryParseDateForSelectedYear(textBox.Text, out date, true))
                {
                    e.Handled = true;
                    return;
                }
                textBox.Text = date.ToString(AppSettings.DateFormat);
            }

            RefreshSearchResults(true);
            e.Handled = true;
        }

        private void AmountTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            SaveCurrentEntry();
            e.Handled = true;
        }

        private FlowDocument CreatePrintDocument(List<AidEntry> data, DateTime? fromDate, DateTime? toDate)
        {
            var document = new FlowDocument
            {
                FlowDirection = FlowDirection.RightToLeft,
                FontFamily = new FontFamily("Arial")
            };

            document.Blocks.Add(new Paragraph(new Run("تقرير المساعدات"))
            {
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center
            });

            string project = SearchProjectComboBox.SelectedItem?.ToString() ?? "جميع المشاريع";
            string quick = QuickSearchTextBox.Text?.Trim();
            string quickPart = string.IsNullOrWhiteSpace(quick) ? string.Empty : "    بحث: " + quick;

            document.Blocks.Add(new Paragraph(new Run(
                $"السنة المالية: {YearComboBox.SelectedItem}    النوع: {project}    " +
                $"من: {(fromDate.HasValue ? fromDate.Value.ToString(AppSettings.DateFormat) : "البداية")}    " +
                $"إلى: {(toDate.HasValue ? toDate.Value.ToString(AppSettings.DateFormat) : "النهاية")}{quickPart}"))
            {
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            });

            var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
            document.Blocks.Add(table);

            table.Columns.Add(new TableColumn { Width = new GridLength(1.3, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(0.8, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(0.8, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(0.8, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1.1, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(0.8, GridUnitType.Star) });

            var headerGroup = new TableRowGroup();
            table.RowGroups.Add(headerGroup);
            var header = new TableRow { Background = Brushes.LightGray, FontWeight = FontWeights.Bold };
            headerGroup.Rows.Add(header);
            header.Cells.Add(CreatePrintCell("المشروع", true));
            header.Cells.Add(CreatePrintCell("السند", true));
            header.Cells.Add(CreatePrintCell("التاريخ", true));
            header.Cells.Add(CreatePrintCell("المتبرع", true));
            header.Cells.Add(CreatePrintCell("الكمية", true));
            header.Cells.Add(CreatePrintCell("البيان/النوع", true));
            header.Cells.Add(CreatePrintCell("المبلغ", true));

            var dataGroup = new TableRowGroup();
            table.RowGroups.Add(dataGroup);
            foreach (AidEntry item in data)
            {
                var row = new TableRow();
                dataGroup.Rows.Add(row);
                row.Cells.Add(CreatePrintCell(item.ProjectName));
                row.Cells.Add(CreatePrintCell(item.VoucherNo));
                row.Cells.Add(CreatePrintCell(item.Date.ToString(AppSettings.DateFormat)));
                row.Cells.Add(CreatePrintCell(item.DonorName));
                row.Cells.Add(CreatePrintCell(item.Quantity == 0 ? string.Empty : item.Quantity.ToString("N0")));
                row.Cells.Add(CreatePrintCell(item.DonationType));
                row.Cells.Add(CreatePrintCell(item.Amount == 0 ? string.Empty : item.Amount.ToString("N3")));
            }

            decimal totalAmount = data.Sum(x => x.Amount);
            int totalQuantity = data.Sum(x => x.Quantity);
            document.Blocks.Add(new Paragraph(new Run(
                $"عدد السجلات: {data.Count:N0}    إجمالي المبلغ: {totalAmount:N3}    إجمالي الكمية/العدد: {totalQuantity:N0}"))
            {
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 12, 0, 0),
                TextAlignment = TextAlignment.Right
            });

            return document;
        }

        private static TableCell CreatePrintCell(string text, bool header = false)
        {
            var paragraph = new Paragraph(new Run(text ?? string.Empty)) { Margin = new Thickness(2) };
            if (header) paragraph.FontWeight = FontWeights.Bold;
            return new TableCell(paragraph)
            {
                Padding = new Thickness(4),
                BorderBrush = header ? Brushes.Gray : Brushes.Gainsboro,
                BorderThickness = new Thickness(0, 0, 1, 1)
            };
        }
    }
}
