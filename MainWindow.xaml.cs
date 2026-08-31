using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

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

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            int? year = SelectFiscalYearForExport();
            if (!year.HasValue) return;

            var dialog = new SaveFileDialog
            {
                Title = "حفظ تقرير السنة المالية",
                Filter = "ملف Excel XML (*.xml)|*.xml",
                FileName = $"تقرير-المحاسبة-{year.Value}.xml",
                AddExtension = true,
                DefaultExt = ".xml"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                ReportExportService.ExportYearToExcelXml(year.Value, dialog.FileName);

                if (MessageBox.Show(
                    "تم تصدير تقرير السنة بنجاح.\n\nهل تريد فتح الملف الآن؟",
                    "تم التصدير",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    Process.Start(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "تعذر تصدير التقرير إلى Excel.\n\n" + ex.Message,
                    "خطأ في التصدير",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private int? SelectFiscalYearForExport()
        {
            var combo = new ComboBox
            {
                Width = 140,
                Height = 32,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            FiscalYearHelper.PopulateYears(combo);
            combo.SelectedItem = FiscalYearHelper.CurrentYear.ToString();
            if (combo.SelectedItem == null && combo.Items.Count > 0) combo.SelectedIndex = combo.Items.Count - 1;

            var okButton = new Button { Content = "تصدير", Width = 100, Height = 34, Margin = new Thickness(5) };
            var cancelButton = new Button { Content = "إلغاء", Width = 100, Height = 34, Margin = new Thickness(5) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);

            var content = new StackPanel { Margin = new Thickness(20) };
            content.Children.Add(new TextBlock
            {
                Text = "اختر السنة المالية المراد تصديرها:",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12),
                TextAlignment = TextAlignment.Center
            });
            content.Children.Add(combo);
            content.Children.Add(buttons);

            var window = new Window
            {
                Title = "تصدير تقرير Excel",
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                SizeToContent = SizeToContent.WidthAndHeight,
                FlowDirection = FlowDirection.RightToLeft,
                Content = content
            };

            int? result = null;
            okButton.Click += (s, e) =>
            {
                if (int.TryParse(combo.SelectedItem?.ToString(), out int selectedYear))
                {
                    result = selectedYear;
                    window.DialogResult = true;
                }
            };
            cancelButton.Click += (s, e) => window.DialogResult = false;

            return window.ShowDialog() == true ? result : null;
        }

        private void AuditLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var grid = new DataGrid
                {
                    AutoGenerateColumns = false,
                    IsReadOnly = true,
                    CanUserAddRows = false,
                    ItemsSource = AuditService.LoadRecent(1000),
                    Margin = new Thickness(10),
                    HeadersVisibility = DataGridHeadersVisibility.Column,
                    GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
                };

                grid.Columns.Add(CreateTextColumn("الوقت", "EventTime", 150));
                grid.Columns.Add(CreateTextColumn("العملية", "Action", 80));
                grid.Columns.Add(CreateTextColumn("القسم", "EntityName", 130));
                grid.Columns.Add(CreateTextColumn("رقم السجل", "RecordId", 90));
                grid.Columns.Add(CreateTextColumn("التفاصيل", "Details", 360));
                grid.Columns.Add(CreateTextColumn("المستخدم", "UserName", 120));
                grid.Columns.Add(CreateTextColumn("الجهاز", "MachineName", 130));

                var window = new Window
                {
                    Title = "سجل التعديلات والحذف",
                    Owner = this,
                    Width = 1150,
                    Height = 680,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    FlowDirection = FlowDirection.RightToLeft,
                    Content = grid
                };

                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "تعذر قراءة سجل التعديلات.\n\n" + ex.Message,
                    "خطأ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static DataGridTextColumn CreateTextColumn(string header, string propertyName, double width)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(propertyName),
                Width = width
            };
        }

        private void Backup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string backupPath = DatabaseService.CreateBackup("manual");
                if (string.IsNullOrWhiteSpace(backupPath))
                {
                    MessageBox.Show("لا توجد قاعدة بيانات لنسخها حالياً.", "نسخة احتياطية", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                MessageBox.Show(
                    "تم إنشاء النسخة الاحتياطية بنجاح.\n\n" + backupPath,
                    "تم النسخ الاحتياطي",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "تعذر إنشاء النسخة الاحتياطية.\n" + ex.Message,
                    "خطأ في النسخ الاحتياطي",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "اختر نسخة قاعدة البيانات المراد استعادتها",
                Filter = "ملفات قاعدة البيانات (*.db)|*.db|جميع الملفات (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (Directory.Exists(DatabaseService.BackupDirectory))
            {
                dialog.InitialDirectory = DatabaseService.BackupDirectory;
            }

            if (dialog.ShowDialog() != true) return;

            if (!DatabaseService.IsValidAccountingDatabase(dialog.FileName))
            {
                MessageBox.Show(
                    "الملف المحدد ليس نسخة صالحة لقاعدة بيانات هذا البرنامج.",
                    "نسخة غير صالحة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show(
                "سيتم استبدال قاعدة البيانات الحالية بالنسخة المختارة.\n\nسيأخذ البرنامج نسخة احتياطية من الوضع الحالي أولاً، ثم يعيد التشغيل.\n\nهل تريد المتابعة؟",
                "تأكيد الاستعادة",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                DatabaseService.RestoreBackup(dialog.FileName);

                MessageBox.Show(
                    "تمت استعادة قاعدة البيانات بنجاح. سيتم إعادة تشغيل البرنامج الآن.",
                    "تمت الاستعادة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                string executable = Assembly.GetEntryAssembly()?.Location;
                if (!string.IsNullOrWhiteSpace(executable) && File.Exists(executable))
                {
                    Process.Start(executable);
                }

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "تعذر استعادة النسخة. تمت محاولة إعادة قاعدة البيانات السابقة تلقائياً.\n\n" + ex.Message,
                    "فشل الاستعادة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
