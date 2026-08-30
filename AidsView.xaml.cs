using AccountingApp;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Printing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace AccountingApp
{
    public partial class AidsView : UserControl
    {
        private readonly string _connectionString = DatabaseService.ConnectionString;
        private AidEntry _aidToEdit = null;
        private readonly List<string> _projectNames = new List<string> {
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
            // ربط التبويبات بالمشاريع برمجياً
            for (int i = 0; i < _projectNames.Count && i < AidEntryTabControl.Items.Count; i++)
            {
                ((TabItem)AidEntryTabControl.Items[i]).Header = _projectNames[i];
            }
            PopulateComboBoxes();
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
        private void AidsView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            FiscalYearHelper.SelectCurrentYear(YearComboBox);

            // استدعاء الدالة الجديدة لضبط تواريخ البحث بناءً على السنة المختارة تلقائياً
            ResetDateFieldsToSelectedYear();

            RefreshSummaryGrid();
            LoadDetailsForCurrentProject();
        }

        private void YearComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                ResetDateFieldsToSelectedYear();
                RefreshSummaryGrid();
                LoadDetailsForCurrentProject();
            }
        }

        private void Search_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            RefreshSummaryGrid();
        }

        private void ShowAll_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            SearchProjectComboBox.SelectedItem = "جميع المشاريع";
            ResetDateFieldsToSelectedYear();
            RefreshSummaryGrid();
        }

        private void ProjectsSummaryDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ProjectsSummaryDataGrid.SelectedItem != null)
            {
                dynamic currentProject = ProjectsSummaryDataGrid.SelectedItem;
                string projectName = currentProject.ProjectName;

                foreach (System.Windows.Controls.TabItem tab in AidEntryTabControl.Items)
                {
                    if (tab.Header.ToString() == projectName)
                    {
                        AidEntryTabControl.SelectedItem = tab;
                        break;
                    }
                }

                if (SearchProjectComboBox != null)
                {
                    SearchProjectComboBox.SelectedItem = projectName;
                }
            }

            DateTime? fromDate = null, toDate = null;
            if (TryParseDate(SearchFromDateTextBox.Text, out DateTime fromDt, false)) fromDate = fromDt;
            if (TryParseDate(SearchToDateTextBox.Text, out DateTime toDt, false)) toDate = toDt;

            if (ProjectsSummaryDataGrid.SelectedItem is ProjectSummary selectedProject)
            {
                AidsDataGrid.ItemsSource = LoadAidDetails(selectedProject.ProjectName, fromDate, toDate);
                {

                }



                switch (selectedProject.ProjectName)
                {
                    case "الطرود الغذائية": QuantityColumn.Header = "عدد الطرود"; DonationTypeColumn.Header = "نوع التبرع"; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Visible; break;
                    case "الملابس والأحذية": QuantityColumn.Header = "عدد الأسر"; DonationTypeColumn.Header = ""; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Collapsed; break;
                    case "معونة الشتاء": QuantityColumn.Header = "عدد الأسر"; DonationTypeColumn.Header = "نوع التبرع"; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Visible; break;
                    case "الحقيبة المدرسية": QuantityColumn.Header = "عدد الحقائب"; DonationTypeColumn.Header = "عدد الأسر"; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Visible; break;
                    case "كسوة العيد": case "الأضاحي": QuantityColumn.Header = "عدد الأسر"; DonationTypeColumn.Header = ""; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Collapsed; break;
                    case "إفطار صائم": QuantityColumn.Header = "عدد الوجبات"; DonationTypeColumn.Header = "عدد الأسر"; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Visible; break;
                    case "أثاث منازل للفقراء": QuantityColumn.Header = "عدد القطع"; DonationTypeColumn.Header = "البيان"; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Visible; break;
                    case "مواد مستهلكة للفقراء": QuantityColumn.Header = "الكمية"; DonationTypeColumn.Header = "البيان"; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Visible; break;
                    case "نذور وكفارات": QuantityColumn.Header = "العدد"; DonationTypeColumn.Header = "البيان"; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Visible; break;
                    case "أصول ثابتة": QuantityColumn.Header = "العدد"; DonationTypeColumn.Header = "البيان"; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Visible; break;
                    case "مواد مستهلكة للمركز": QuantityColumn.Header = "الكمية"; DonationTypeColumn.Header = "البيان"; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Visible; break;
                    case "أدوية ومستلزمات طبية": QuantityColumn.Header = "الكمية"; DonationTypeColumn.Header = "البيان"; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Visible; break;
                    default: QuantityColumn.Header = "الكمية/العدد"; DonationTypeColumn.Header = "نوع التبرع"; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Visible; break;
                }
            }
            else
            {
                AidsDataGrid.ItemsSource = null;
                QuantityColumn.Header = "الكمية/العدد";
                DonationTypeColumn.Header = "نوع التبرع";
                QuantityColumn.Visibility = Visibility.Visible;
                DonationTypeColumn.Visibility = Visibility.Visible;
            }
        }

        // دالة جديدة لتحميل تفاصيل المشروع الحالي
        private void LoadDetailsForCurrentProject()
        {
            if (AidEntryTabControl.SelectedItem == null) return;
            string projectName = (AidEntryTabControl.SelectedItem as TabItem)?.Header?.ToString();

            DateTime? fromDate = null, toDate = null;
            if (TryParseDate(SearchFromDateTextBox.Text, out DateTime fromDt, false)) fromDate = fromDt;
            if (TryParseDate(SearchToDateTextBox.Text, out DateTime toDt, false)) toDate = toDt;

            var details = LoadAidDetails(projectName, fromDate, toDate);
            AidsDataGrid.ItemsSource = details;

            // تحديث عناوين الأعمدة حسب المشروع
            switch (projectName)
            {
                case "الطرود الغذائية": QuantityColumn.Header = "عدد الطرود"; DonationTypeColumn.Header = "نوع التبرع"; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Visible; break;
                case "الملابس والأحذية": QuantityColumn.Header = "عدد الأسر"; DonationTypeColumn.Header = ""; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Collapsed; break;
                case "معونة الشتاء": QuantityColumn.Header = "عدد الأسر"; DonationTypeColumn.Header = "نوع التبرع"; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Visible; break;
                case "الحقيبة المدرسية": QuantityColumn.Header = "عدد الحقائب"; DonationTypeColumn.Header = "عدد الأسر"; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Visible; break;
                case "كسوة العيد": case "الأضاحي": QuantityColumn.Header = "عدد الأسر"; DonationTypeColumn.Header = ""; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Collapsed; break;
                case "إفطار صائم": QuantityColumn.Header = "عدد الوجبات"; DonationTypeColumn.Header = "عدد الأسر"; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Visible; break;
                case "أثاث منازل للفقراء": QuantityColumn.Header = "عدد القطع"; DonationTypeColumn.Header = "البيان"; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Visible; break;
                default: QuantityColumn.Header = "الكمية/العدد"; DonationTypeColumn.Header = "نوع التبرع"; QuantityColumn.Visibility = Visibility.Visible; DonationTypeColumn.Visibility = Visibility.Visible; break;
            }
        }

        private void AidEntryTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || AidEntryTabControl.SelectedItem == null) return;
            string projectName = (AidEntryTabControl.SelectedItem as TabItem)?.Header?.ToString();

            // التفاعل: عند الضغط على تبويب، يتم تحديد المشروع في الملخص تلقائياً لعرض تفاصيله
            var summaryItem = ProjectsSummaryDataGrid.Items.OfType<ProjectSummary>().FirstOrDefault(p => p.ProjectName == projectName);
            if (summaryItem != null)
            {
                ProjectsSummaryDataGrid.SelectedItem = summaryItem;
            }

            // إضافة: تحميل تفاصيل المشروع المحدد في الشبكة السفلية
            LoadDetailsForCurrentProject();

            UpdateTabFieldLabels(projectName);
        }

        private void UpdateTabFieldLabels(string projectName)
        {
            var cp = (ContentPresenter)AidEntryTabControl.Template.FindName("PART_SelectedContentHost", AidEntryTabControl);
            if (cp == null) return;

            var content = cp.ContentTemplate;
            if (content == null) return;

            var qtyLabel = content.FindName("CommonQtyLabel", cp) as TextBlock;
            var typeLabel = content.FindName("CommonTypeLabel", cp) as TextBlock;
            var typeInput = content.FindName("CommonTypeInput", cp) as TextBox;
            var donorLabel = content.FindName("CommonDonorLabel", cp) as TextBlock;
            var amountLabel = content.FindName("CommonAmountLabel", cp) as TextBlock;

            if (qtyLabel == null || typeLabel == null) return;
            
            qtyLabel.Visibility = Visibility.Visible;
            typeLabel.Visibility = Visibility.Visible;
            if (typeInput != null) typeInput.Visibility = Visibility.Visible;
            if (donorLabel != null) donorLabel.Visibility = Visibility.Visible;
            if (amountLabel != null) amountLabel.Visibility = Visibility.Visible;

            string commonType = "البيان:";

            switch (projectName)
            {
                case "الطرود الغذائية": qtyLabel.Text = "عدد الطرود:"; typeLabel.Text = "نوع التبرع:"; break;
                case "الملابس والأحذية": qtyLabel.Text = "عدد الأسر:"; typeLabel.Text = ""; if (typeInput != null) typeInput.Visibility = Visibility.Collapsed; break;
                case "معونة الشتاء": qtyLabel.Text = "عدد الأسر:"; typeLabel.Text = "نوع التبرع:"; break;
                case "الحقيبة المدرسية": qtyLabel.Text = "عدد الحقائب:"; typeLabel.Text = "عدد الأسر:"; break;
                case "كسوة العيد": case "الأضاحي": qtyLabel.Text = "عدد الأسر:"; typeLabel.Text = ""; if (typeInput != null) typeInput.Visibility = Visibility.Collapsed; break;
                case "إفطار صائم": qtyLabel.Text = "عدد الوجبات:"; typeLabel.Text = "عدد الأسر:"; break;
                case "أثاث منازل للفقراء": qtyLabel.Text = "عدد القطع:"; typeLabel.Text = commonType; break;
                default: qtyLabel.Text = "الكمية:"; typeLabel.Text = commonType; if (typeInput != null) typeInput.Visibility = Visibility.Visible; break;
            }
        }

        private void AidsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void AddAid_Click(object sender, RoutedEventArgs e)
        {
            if (AidEntryTabControl.SelectedItem == null) return;
            string projectName = (AidEntryTabControl.SelectedItem as TabItem)?.Header?.ToString();
            var entry = new AidEntry { ProjectName = projectName, Year = FiscalYearHelper.GetSelectedYear(YearComboBox) };

            try
            {
                bool success = PopulateAidEntry(entry, projectName);
                if (!success) return;
                if (!FiscalYearHelper.ValidateDateInSelectedYear(entry.Date, YearComboBox, "تاريخ المساعدة")) return;
                entry.Year = FiscalYearHelper.GetSelectedYear(YearComboBox);
            }
            catch (FormatException)
            {
                MessageBox.Show("الرجاء التأكد من إدخال أرقام صحيحة في حقلي المبلغ والكمية.", "خطأ في الإدخال", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_aidToEdit == null)
            {
                SaveAidEntry(entry);
                MessageBox.Show("تمت إضافة الإدخال بنجاح.", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                entry.Id = _aidToEdit.Id;
                UpdateAidEntry(entry);
                MessageBox.Show("تم تحديث الإدخال بنجاح.", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            RefreshSummaryGrid();
            ClearInputFields();
            ExitEditMode();
            LoadDetailsForCurrentProject();
        }

        private void EditAid_Click(object sender, RoutedEventArgs e)
        {
            var selectedAid = (sender as FrameworkElement)?.DataContext as AidEntry;
            if (selectedAid != null)
            {
                _aidToEdit = selectedAid;
                
                foreach (TabItem tab in AidEntryTabControl.Items)
                {
                    if (tab.Header?.ToString() == selectedAid.ProjectName)
                    {
                        AidEntryTabControl.SelectedItem = tab;
                        break;
                    }
                }
                
                PopulateUIForEdit(selectedAid);
                SetButtonContent(selectedAid.ProjectName, "تحديث الإدخال");
            }
            else
            {
                MessageBox.Show("لم يتم العثور على بيانات السجل المحدد.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteAid_Click(object sender, RoutedEventArgs e)
        {
            var selectedAid = (sender as FrameworkElement)?.DataContext as AidEntry;
            if (selectedAid != null)
            {
                if (MessageBox.Show("هل أنت متأكد من رغبتك في حذف هذا السجل؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    DeleteAidEntry(selectedAid.Id);
                    RefreshSummaryGrid();
                    LoadDetailsForCurrentProject();
                }
            }
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            string filterProject = SearchProjectComboBox.SelectedItem as string;
            if (filterProject == "جميع المشاريع") filterProject = null;

            DateTime? fromDate = null, toDate = null;
            if (TryParseDate(SearchFromDateTextBox.Text, out DateTime fromDt, false)) fromDate = fromDt;
            if (TryParseDate(SearchToDateTextBox.Text, out DateTime toDt, false)) toDate = toDt;

            var allEntries = LoadAllAidDetailsForPrinting(filterProject, fromDate, toDate);
            if (!allEntries.Any()) { MessageBox.Show("لا توجد بيانات للطباعة."); return; }
            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                FlowDocument doc = CreatePrintDocument(allEntries, filterProject, fromDate, toDate);
                doc.PagePadding = new Thickness(50);
                doc.ColumnWidth = printDialog.PrintableAreaWidth;
                printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "تقرير المساعدات");
            }
        }
        #endregion

        #region Database Interaction
        private void SaveAidEntry(AidEntry entry)
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var sql = "INSERT INTO Aids (ProjectName, DonorName, Date, Amount, Quantity, DonationType, Year) VALUES (@ProjectName, @DonorName, @Date, @Amount, @Quantity, @DonationType, @Year)";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ProjectName", entry.ProjectName);
                    cmd.Parameters.AddWithValue("@DonorName", (object)entry.DonorName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Date", entry.Date.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@Amount", entry.Amount);
                    cmd.Parameters.AddWithValue("@Quantity", entry.Quantity);
                    cmd.Parameters.AddWithValue("@DonationType", (object)entry.DonationType ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Year", entry.Year);
                    cmd.ExecuteNonQuery();
                }
            }
        }
        private void UpdateAidEntry(AidEntry entry)
        {
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    var sql = "UPDATE Aids SET ProjectName = @ProjectName, DonorName = @DonorName, Date = @Date, Amount = @Amount, Quantity = @Quantity, DonationType = @DonationType, Year = @Year WHERE Id = @Id";
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ProjectName", entry.ProjectName);
                        cmd.Parameters.AddWithValue("@DonorName", (object)entry.DonorName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Date", entry.Date.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@Amount", entry.Amount);
                        cmd.Parameters.AddWithValue("@Quantity", entry.Quantity);
                        cmd.Parameters.AddWithValue("@DonationType", (object)entry.DonationType ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Year", entry.Year);
                        cmd.Parameters.AddWithValue("@Id", entry.Id);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (SqliteException ex)
            {
                MessageBox.Show("حدث خطأ أثناء تحديث البيانات: " + ex.Message, "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void DeleteAidEntry(int id)
        {
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    var sql = "DELETE FROM Aids WHERE Id = @Id";
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (SqliteException ex)
            {
                MessageBox.Show("حدث خطأ أثناء حذف البيانات: " + ex.Message, "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<ProjectSummary> LoadProjectsSummary(string filterProject, DateTime? fromDate, DateTime? toDate)
        {
            var list = new List<ProjectSummary>();
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                int selectedYear = FiscalYearHelper.GetSelectedYear(YearComboBox);
                var sql = new StringBuilder("SELECT ProjectName, SUM(Amount), SUM(Quantity) FROM Aids WHERE Year = @Year");
                var parameters = new Dictionary<string, object> { { "@Year", selectedYear } };
                if (!string.IsNullOrEmpty(filterProject) && filterProject != "جميع المشاريع") { sql.Append(" AND ProjectName = @ProjectName"); parameters.Add("@ProjectName", filterProject); }
                if (fromDate.HasValue) { sql.Append(" AND date(Date) >= date(@FromDate)"); parameters.Add("@FromDate", fromDate.Value.ToString("yyyy-MM-dd")); }
                if (toDate.HasValue) { sql.Append(" AND date(Date) <= date(@ToDate)"); parameters.Add("@ToDate", toDate.Value.ToString("yyyy-MM-dd")); }

                sql.Append(" GROUP BY ProjectName");
                using (var cmd = new SqliteCommand(sql.ToString(), conn))
                {
                    foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Key, p.Value);
                    using (var reader = cmd.ExecuteReader()) { while (reader.Read()) list.Add(new ProjectSummary { ProjectName = reader.GetString(0), TotalAmount = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetDouble(1)), TotalQuantity = reader.IsDBNull(2) ? 0 : reader.GetInt32(2) }); }
                }
            }
            return list;
        }

        private List<AidEntry> LoadAidDetails(string projectName, DateTime? fromDate, DateTime? toDate)
        {
            var list = new List<AidEntry>();
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                int selectedYear = FiscalYearHelper.GetSelectedYear(YearComboBox);
                var sql = new StringBuilder("SELECT Id, DonorName, Date, Amount, Quantity, DonationType, ProjectName FROM Aids WHERE ProjectName = @ProjectName AND Year = @Year");
                var parameters = new Dictionary<string, object> { { "@ProjectName", projectName }, { "@Year", selectedYear } };
                if (fromDate.HasValue) { sql.Append(" AND date(Date) >= date(@FromDate)"); parameters.Add("@FromDate", fromDate.Value.ToString("yyyy-MM-dd")); }
                if (toDate.HasValue) { sql.Append(" AND date(Date) <= date(@ToDate)"); parameters.Add("@ToDate", toDate.Value.ToString("yyyy-MM-dd")); }
                sql.Append(" ORDER BY Date DESC");
                using (var cmd = new SqliteCommand(sql.ToString(), conn))
                {
                    foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Key, p.Value);
                    using (var reader = cmd.ExecuteReader()) { while (reader.Read()) list.Add(new AidEntry { Id = reader.GetInt32(0), DonorName = reader.IsDBNull(1) ? "" : reader.GetString(1), Date = DateTime.Parse(reader.GetString(2)), Amount = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetDouble(3)), Quantity = reader.IsDBNull(4) ? 0 : reader.GetInt32(4), DonationType = reader.IsDBNull(5) ? "" : reader.GetString(5), ProjectName = reader.GetString(6) }); }
                }
            }
            return list;
        }

        private List<AidEntry> LoadAllAidDetailsForPrinting(string projectFilter, DateTime? fromDate, DateTime? toDate)
        {
            var list = new List<AidEntry>();
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                int selectedYear = FiscalYearHelper.GetSelectedYear(YearComboBox);
                var sql = new StringBuilder("SELECT ProjectName, DonorName, Date, Amount, Quantity, DonationType FROM Aids WHERE Year = @Year");
                var parameters = new Dictionary<string, object> { { "@Year", selectedYear } };
                if (!string.IsNullOrEmpty(projectFilter)) { sql.Append(" AND ProjectName = @ProjectName"); parameters.Add("@ProjectName", projectFilter); }
                if (fromDate.HasValue) { sql.Append(" AND date(Date) >= date(@FromDate)"); parameters.Add("@FromDate", fromDate.Value.ToString("yyyy-MM-dd")); }
                if (toDate.HasValue) { sql.Append(" AND date(Date) <= date(@ToDate)"); parameters.Add("@ToDate", toDate.Value.ToString("yyyy-MM-dd")); }
                sql.Append(" ORDER BY ProjectName, Date");
                using (var cmd = new SqliteCommand(sql.ToString(), conn))
                {
                    foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Key, p.Value);
                    using (var reader = cmd.ExecuteReader()) { while (reader.Read()) list.Add(new AidEntry { ProjectName = reader.GetString(0), DonorName = reader.IsDBNull(1) ? "" : reader.GetString(1), Date = DateTime.Parse(reader.GetString(2)), Amount = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetDouble(3)), Quantity = reader.IsDBNull(4) ? 0 : reader.GetInt32(4), DonationType = reader.IsDBNull(5) ? "" : reader.GetString(5) }); }
                }
            }
            return list;
        }
        #endregion

        #region UI & Helpers
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
        }

        private bool PopulateAidEntry(AidEntry entry, string projectName)
        {
            var fields = GetAidFields(projectName);

            if (fields == null || fields.Date == null || fields.Donor == null || fields.Quantity == null || fields.Amount == null)
            {
                MessageBox.Show("لم يتم العثور على حقول الإدخال لهذا المشروع.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!TryParseDate(fields.Date.Text, out DateTime parsedDate)) return false;

            entry.Date = parsedDate;
            entry.DonorName = fields.Donor.Text.Trim();
            entry.Quantity = ParseInt(fields.Quantity.Text);
            entry.Amount = ParseDecimal(fields.Amount.Text);

            string typeText = fields.Type?.Text.Trim();
            if ((projectName == "إفطار صائم" || projectName == "الحقيبة المدرسية") && !string.IsNullOrWhiteSpace(typeText))
                entry.DonationType = $"أسر: {typeText}";
            else
                entry.DonationType = typeText;

            return true;
        }

        private void PopulateUIForEdit(AidEntry entry)
        {
            var fields = GetAidFields(entry.ProjectName);

            if (fields == null || fields.Date == null || fields.Donor == null || fields.Quantity == null || fields.Amount == null)
                return;

            fields.Date.Text = entry.Date.ToString(AppSettings.DateFormat);
            fields.Donor.Text = entry.DonorName;
            fields.Quantity.Text = entry.Quantity == 0 ? string.Empty : entry.Quantity.ToString();
            fields.Amount.Text = entry.Amount == 0 ? string.Empty : entry.Amount.ToString("N3");

            if (fields.Type != null)
            {
                if (entry.DonationType != null && (entry.ProjectName == "إفطار صائم" || entry.ProjectName == "الحقيبة المدرسية"))
                    fields.Type.Text = entry.DonationType.Replace("أسر: ", "").Trim();
                else
                    fields.Type.Text = entry.DonationType;
            }
        }

        private int ParseInt(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return int.Parse(text.Trim(), NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.CurrentCulture);
        }

        private decimal ParseDecimal(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            string normalizedText = text.Trim();
            if (decimal.TryParse(normalizedText, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal value)) return value;
            return decimal.Parse(normalizedText, NumberStyles.Any, CultureInfo.InvariantCulture);
        }

        private void RefreshSummaryGrid()
        {
            if (YearComboBox.SelectedItem == null) return;

            DateTime? fromDate = null, toDate = null;
            if (TryParseDate(SearchFromDateTextBox.Text, out DateTime fromDt, false)) fromDate = fromDt;
            if (TryParseDate(SearchToDateTextBox.Text, out DateTime toDt, false)) toDate = toDt;
            string filterProject = SearchProjectComboBox.SelectedItem as string;
            var selectedProjectSummary = ProjectsSummaryDataGrid.SelectedItem as ProjectSummary;
            string selectedProjectName = selectedProjectSummary?.ProjectName;

            // تحميل الملخص
            var projects = LoadProjectsSummary(filterProject, fromDate, toDate);
            ProjectsSummaryDataGrid.ItemsSource = null;
            ProjectsSummaryDataGrid.ItemsSource = projects;

            // إعادة تحديد المشروع السابق
            if (!string.IsNullOrEmpty(selectedProjectName))
            {
                var itemToReselect = ProjectsSummaryDataGrid.Items.OfType<ProjectSummary>().FirstOrDefault(p => p.ProjectName == selectedProjectName);
                if (itemToReselect != null)
                {
                    ProjectsSummaryDataGrid.SelectedItem = itemToReselect;
                }
            }

            if (ProjectsSummaryDataGrid.SelectedItem == null && ProjectsSummaryDataGrid.Items.Count > 0)
            {
                ProjectsSummaryDataGrid.SelectedIndex = 0;
            }
            else if (ProjectsSummaryDataGrid.Items.Count == 0)
            {
                AidsDataGrid.ItemsSource = null;
            }
        }

        private void ClearInputFields()
        {
            string projectName = (AidEntryTabControl.SelectedItem as TabItem)?.Header?.ToString();
            var fields = GetAidFields(projectName);

            fields?.Date?.Clear();
            fields?.Donor?.Clear();
            fields?.Type?.Clear();
            fields?.Quantity?.Clear();
            fields?.Amount?.Clear();
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
                    TraversalRequest request = new TraversalRequest(FocusNavigationDirection.Next);
                    request.Wrapped = true;
                    ((UIElement)sender).MoveFocus(request);
                }
            }
        }

        private void MoveFocusOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var request = new TraversalRequest(FocusNavigationDirection.Next);
                if (Keyboard.FocusedElement is UIElement elementWithFocus && elementWithFocus.MoveFocus(request))
                {
                    e.Handled = true;
                }
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
            var doc = new FlowDocument { FlowDirection = FlowDirection.RightToLeft, FontFamily = new FontFamily("Arial") };
            string title = "تقرير المساعدات" + (!string.IsNullOrEmpty(project) ? $" - {project}" : "");
            doc.Blocks.Add(new Paragraph(new Run(title)) { FontSize = 20, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center });
            doc.Blocks.Add(new Paragraph(new Run($"من: {(fromDate.HasValue ? fromDate.Value.ToString(AppSettings.DateFormat) : "البداية")}  إلى: {(toDate.HasValue ? toDate.Value.ToString(AppSettings.DateFormat) : "النهاية")}")) { FontSize = 12, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 20) });
            var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
            doc.Blocks.Add(table);

            table.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1.2, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

            var headerRowGroup = new TableRowGroup();
            table.RowGroups.Add(headerRowGroup);
            var headerRow = new TableRow { Background = Brushes.LightGray, FontWeight = FontWeights.Bold };
            headerRowGroup.Rows.Add(headerRow);

            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("المشروع"))) { Padding = new Thickness(5), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0, 0, 1, 1) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("المتبرع"))) { Padding = new Thickness(5), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0, 0, 1, 1) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("التاريخ"))) { Padding = new Thickness(5), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0, 0, 1, 1) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("الكمية/النوع"))) { Padding = new Thickness(5), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0, 0, 1, 1) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("المبلغ"))) { Padding = new Thickness(5), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0, 0, 0, 1) });

            var dataGroup = new TableRowGroup();
            table.RowGroups.Add(dataGroup);
            foreach (var item in data)
            {
                var dataRow = new TableRow();
                dataGroup.Rows.Add(dataRow);
                dataRow.Cells.Add(new TableCell(new Paragraph(new Run(item.ProjectName))) { Padding = new Thickness(5), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 1, 1) });
                dataRow.Cells.Add(new TableCell(new Paragraph(new Run(item.DonorName))) { Padding = new Thickness(5), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 1, 1) });
                dataRow.Cells.Add(new TableCell(new Paragraph(new Run(item.Date.ToString(AppSettings.DateFormat)))) { Padding = new Thickness(5), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 1, 1) });
                string qty = item.Quantity > 0 ? item.Quantity.ToString() : "";
                if (!string.IsNullOrWhiteSpace(item.DonationType))
                {
                    qty += string.IsNullOrEmpty(qty) ? item.DonationType : $" ({item.DonationType})";
                }
                dataRow.Cells.Add(new TableCell(new Paragraph(new Run(qty))) { Padding = new Thickness(5), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 1, 1) });
                dataRow.Cells.Add(new TableCell(new Paragraph(new Run(item.Amount > 0 ? item.Amount.ToString("N3") : ""))) { Padding = new Thickness(5), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 0, 1) });

            }
            return doc;
        }



        private void AddUpdate_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            AddAid_Click(sender, e);
        }
        #endregion
    }
}
