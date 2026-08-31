using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;

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
