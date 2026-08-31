using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;

namespace AccountingApp
{
    /// <summary>
    /// فئة مركزية للتعامل مع قاعدة البيانات
    /// توفر Connection String مشترك ودوال مساعدة
    /// </summary>
    public static class DatabaseService
    {
        private const string DatabaseFileName = "invoices.db";
        private static bool _isInitialized;

        public static string DatabasePath
        {
            get
            {
                var executableDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DatabaseFileName);
                if (File.Exists(executableDirectoryPath))
                {
                    return executableDirectoryPath;
                }

                var workingDirectoryPath = Path.Combine(Environment.CurrentDirectory, DatabaseFileName);
                if (File.Exists(workingDirectoryPath))
                {
                    return workingDirectoryPath;
                }

                return executableDirectoryPath;
            }
        }

        public static string ConnectionString
        {
            get
            {
                return new SqliteConnectionStringBuilder
                {
                    DataSource = DatabasePath
                }.ToString();
            }
        }

        /// <summary>
        /// إنشاء اتصال جديد بقاعدة البيانات
        /// </summary>
        public static SqliteConnection GetConnection()
        {
            return new SqliteConnection(ConnectionString);
        }

        /// <summary>
        /// تحليل التاريخ من نص مع معالجة الأخطاء ومرونة في الصيغ
        /// يدعم الصيغ: d/M, d-M, d/M/yyyy, d-M-yyyy, yyyy/M/d, وغيرها
        /// </summary>
        public static bool TryParseDate(string dateText, out DateTime date, bool showMessage = true, int? defaultYear = null)
        {
            date = default(DateTime);
            if (string.IsNullOrWhiteSpace(dateText))
            {
                if (showMessage) MessageBox.Show("الرجاء إدخال التاريخ.");
                return false;
            }

            dateText = dateText.Trim();

            if (TryParseFlexibleDate(dateText, out date, defaultYear))
            {
                return true;
            }

            if (showMessage)
            {
                MessageBox.Show(
                    $"صيغة التاريخ غير صحيحة.\n\nالصيغ المدعومة:\n• 2/2 أو 2-2 (يستخدم السنة المختارة)\n• 2/2/2026 أو 2-2-2026\n• 02/02/2026\n\nالتاريخ '{dateText}' غير صالح (مثال: 30/2 غير صحيح لأن فبراير لا يحتوي على 30 يوم).",
                    "خطأ في التاريخ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            return false;
        }

        private static bool TryParseFlexibleDate(string dateText, out DateTime date, int? defaultYear = null)
        {
            date = default(DateTime);
            int year = defaultYear ?? DateTime.Now.Year;

            dateText = dateText.Replace('-', '/').Replace('.', '/').Trim();
            dateText = System.Text.RegularExpressions.Regex.Replace(dateText, @"\s+", "");

            if (DateTime.TryParseExact(dateText, AppSettings.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ||
                DateTime.TryParseExact(dateText, "yyyy/M/d", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return true;
            }

            string[] parts = dateText.Split('/');

            if (parts.Length == 2)
            {
                if (int.TryParse(parts[0], out int day) &&
                    int.TryParse(parts[1], out int month) &&
                    IsValidDate(year, month, day))
                {
                    date = new DateTime(year, month, day);
                    return true;
                }
                return false;
            }

            if (parts.Length == 3)
            {
                if (int.TryParse(parts[0], out int day) &&
                    int.TryParse(parts[1], out int month) &&
                    int.TryParse(parts[2], out int parsedYear))
                {
                    if (parsedYear < 100) parsedYear += 2000;

                    if (IsValidDate(parsedYear, month, day))
                    {
                        date = new DateTime(parsedYear, month, day);
                        return true;
                    }
                }
                return false;
            }

            if (DateTime.TryParse(dateText, CultureInfo.GetCultureInfo("ar-JO"), DateTimeStyles.None, out date) ||
                DateTime.TryParse(dateText, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
            {
                return true;
            }

            return false;
        }

        private static bool IsValidDate(int year, int month, int day)
        {
            if (month < 1 || month > 12) return false;

            try
            {
                return day >= 1 && day <= DateTime.DaysInMonth(year, month);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// تهيئة الجداول الأساسية مع Indexes للأداء.
        /// لا يتم حذف أو إعادة إنشاء أي جدول موجود، حفاظاً على توافق قاعدة البيانات القديمة.
        /// </summary>
        public static void InitializeDatabase()
        {
            if (_isInitialized) return;

            using (var conn = GetConnection())
            {
                conn.Open();

                string sql = @"
                CREATE TABLE IF NOT EXISTS Invoices (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    InvoiceNo TEXT,
                    Date TEXT NOT NULL,
                    Description TEXT,
                    Debit REAL,
                    Credit REAL,
                    Year INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS OpeningBalances (
                    Year INTEGER PRIMARY KEY,
                    Balance REAL NOT NULL
                );

                CREATE TABLE IF NOT EXISTS ReceiptVouchers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Date TEXT NOT NULL,
                    BookNo TEXT,
                    FromVoucherNo INTEGER,
                    ToVoucherNo INTEGER,
                    Recipient TEXT,
                    Amount REAL NOT NULL,
                    Year INTEGER NOT NULL,
                    Notes TEXT
                );

                CREATE TABLE IF NOT EXISTS Deposits (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Date TEXT NOT NULL,
                    Amount REAL NOT NULL,
                    DepositorName TEXT,
                    Year INTEGER NOT NULL,
                    Notes TEXT
                );

                CREATE TABLE IF NOT EXISTS Aids (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProjectName TEXT NOT NULL,
                    DonorName TEXT,
                    Date DATETIME NOT NULL,
                    Amount REAL NOT NULL,
                    Quantity INTEGER NOT NULL,
                    DonationType TEXT,
                    Year INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Cars (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CarNumber TEXT NOT NULL UNIQUE
                );

                CREATE TABLE IF NOT EXISTS FuelInvoices (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CarNumber TEXT NOT NULL,
                    InvoiceNumber TEXT,
                    Date TEXT NOT NULL,
                    Amount REAL NOT NULL,
                    IsPaid INTEGER NOT NULL DEFAULT 0,
                    Year INTEGER NOT NULL
                );";

                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }

                string indexesSql = @"
                CREATE INDEX IF NOT EXISTS idx_invoices_year ON Invoices(Year);
                CREATE INDEX IF NOT EXISTS idx_invoices_date ON Invoices(Date);
                CREATE INDEX IF NOT EXISTS idx_invoices_year_date ON Invoices(Year, Date);

                CREATE INDEX IF NOT EXISTS idx_receiptvouchers_year ON ReceiptVouchers(Year);
                CREATE INDEX IF NOT EXISTS idx_receiptvouchers_date ON ReceiptVouchers(Date);
                CREATE INDEX IF NOT EXISTS idx_receiptvouchers_year_date ON ReceiptVouchers(Year, Date);

                CREATE INDEX IF NOT EXISTS idx_deposits_year ON Deposits(Year);
                CREATE INDEX IF NOT EXISTS idx_deposits_date ON Deposits(Date);
                CREATE INDEX IF NOT EXISTS idx_deposits_year_date ON Deposits(Year, Date);

                CREATE INDEX IF NOT EXISTS idx_aids_year ON Aids(Year);
                CREATE INDEX IF NOT EXISTS idx_aids_date ON Aids(Date);
                CREATE INDEX IF NOT EXISTS idx_aids_project ON Aids(ProjectName);
                CREATE INDEX IF NOT EXISTS idx_aids_year_project ON Aids(Year, ProjectName);

                CREATE INDEX IF NOT EXISTS idx_fuelinvoices_year ON FuelInvoices(Year);
                CREATE INDEX IF NOT EXISTS idx_fuelinvoices_date ON FuelInvoices(Date);
                CREATE INDEX IF NOT EXISTS idx_fuelinvoices_car ON FuelInvoices(CarNumber);
                CREATE INDEX IF NOT EXISTS idx_fuelinvoices_year_date ON FuelInvoices(Year, Date);
                ";

                using (var cmd = new SqliteCommand(indexesSql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }

            _isInitialized = true;
        }
    }
}
