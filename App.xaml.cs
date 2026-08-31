using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace AccountingApp
{
    public partial class App : Application
    {
        public App()
        {
            var culture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            DatabaseService.InitializeDatabase();

            try
            {
                DatabaseService.CreateDailyBackup();
            }
            catch (Exception ex)
            {
                LogUnhandledException(new InvalidOperationException("تعذر إنشاء النسخة الاحتياطية اليومية.", ex));
            }

            base.OnStartup(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogUnhandledException(e.Exception);
            MessageBox.Show(
                "حدث خطأ أثناء تشغيل البرنامج. تم حفظ التفاصيل في ملف AccountingApp-error.log بجانب البرنامج.",
                "خطأ في تشغيل البرنامج",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
            Shutdown();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogUnhandledException(e.ExceptionObject as Exception);
        }

        private static void LogUnhandledException(Exception exception)
        {
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AccountingApp-error.log");
                File.AppendAllText(
                    logPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    + Environment.NewLine
                    + (exception?.ToString() ?? "Unknown unhandled exception")
                    + Environment.NewLine
                    + new string('-', 80)
                    + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
