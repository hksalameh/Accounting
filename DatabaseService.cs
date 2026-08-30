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
        /// <param name="dateText">نص التاريخ المراد تحليله</param>
        /// <param name="date">التاريخ المحلل (إذا نجح التحليل)</param>
        /// <param name="showMessage">عرض رسالة خطأ للمستخدم</param>
        /// <param name="defaultYear">السنة الافتراضية (إذا لم يتم إدخال سنة في التاريخ)</param>
        public static bool TryParseDate(string dateText, out DateTime date, bool showMessage = true, int? defaultYear = null)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(dateText))
            {
                if (showMessage) MessageBox.Show("الرجاء إدخال التاريخ.");
                return !showMessage;
            }

            dateText = dateText.Trim();

            // محاولة التحليل بالصيغ المختلفة
            if (TryParseFlexibleDate(dateText, out date, defaultYear))
            {
                return true;
            }

            // إذا فشل كل شيء، نعرض رسالة الخطأ
            if (showMessage)
                MessageBox.Show($"صيغة التاريخ غير صحيحة.\n\nالصيغ المدعومة:\n• 2/2 أو 2-2 (يستخدم السنة الحالية)\n• 2/2/2026 أو 2-2-2026\n• 02/02/2026\n\nالتاريخ '{dateText}' غير صالح (مثال: 30/2 غير صحيح لأن فبراير لا يحتوي على 30 يوم).", 
                    "خطأ في التاريخ", MessageBoxButton.OK, MessageBoxImage.Error);

            return false;
        }

        /// <summary>
        /// تحليل مرن للتاريخ يدعم صيغ متعددة
        /// </summary>
        private static bool TryParseFlexibleDate(string dateText, out DateTime date, int? defaultYear = null)
        {
            date = default;

            // إذا كانت السنة الافتراضية غير محددة، نستخدم السنة الحالية
            int year = defaultYear ?? DateTime.Now.Year;

            // Normalize: استبدال الشرطة بـ /
            dateText = dateText.Replace('-', '/').Replace('.', '/').Trim();

            // إزالة المسافات الزائدة
            dateText = System.Text.RegularExpressions.Regex.Replace(dateText, @"\s+", "");

            // نعالج صيغ التطبيق الواضحة أولاً حتى لا يفسر النظام 3/5 كـ شهر/يوم.
            if (DateTime.TryParseExact(dateText, AppSettings.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ||
                DateTime.TryParseExact(dateText, "yyyy/M/d", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return true;
            }

            // التحليل المرن يعتمد دائماً يوم/شهر[/سنة].
            string[] parts = dateText.Split('/');

            // حالة 1: يوم/شهر فقط (مثل: 2/2 أو 15/3)
            if (parts.Length == 2)
            {
                if (int.TryParse(parts[0], out int day) && int.TryParse(parts[1], out int month))
                {
                    // التحقق من صحة التاريخ
                    if (IsValidDate(year, month, day))
                    {
                        date = new DateTime(year, month, day);
                        return true;
                    }
                }
                return false;
            }

            // حالة 2: يوم/شهر/سنة (مثل: 2/2/2026 أو 2/2/26)
            if (parts.Length == 3)
            {
                if (int.TryParse(parts[0], out int day) && 
                    int.TryParse(parts[1], out int month) && 
                    int.TryParse(parts[2], out int parsedYear))
                {
                    // إذا كانت السنة مكونة من رقمين (مثل 26)
                    if (parsedYear < 100)
                    {
                        parsedYear += 2000;
                    }

                    if (IsValidDate(parsedYear, month, day))
                    {
                        date = new DateTime(parsedYear, month, day);
                        return true;
                    }
                }
                return false;
            }

            // fallback للصيغ النصية أو الطويلة غير الرقمية فقط.
            if (DateTime.TryParse(dateText, CultureInfo.GetCultureInfo("ar-JO"), DateTimeStyles.None, out date) ||
                DateTime.TryParse(dateText, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// التحقق من صحة التاريخ (يوم وشهر وسنة صحيحة)
        /// </summary>
        private static bool IsValidDate(int year, int month, int day)
        {
            // التحقق من الشهر (1-12)
            if (month < 1 || month > 12)
                return false;

            // التحقق من اليوم (يجب أن يكون صالحاً لهذا الشهر)
            int daysInMonth;
            try
            {
                daysInMonth = DateTime.DaysInMonth(year, month);
            }
            catch
            {
                return false;
            }

            return day >= 1 && day <= daysInMonth;
        }

        /// <summary>
        /// تهيئة الجداول الأساسية مع Indexes للأداء
        /// </summary>
        public static void InitializeDatabase()
        {
            if (_isInitialized) return;

            using (var conn = GetConnection())
            {
                conn.Open();
                
                // إنشاء الجداول الأساسية
                string sql = @"
                -- جدول الفواتير
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
                
                -- جدول سندات القبض
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
                
                -- جدول الإيداعات
                CREATE TABLE IF NOT EXISTS Deposits (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT, 
                    Date TEXT NOT NULL, 
                    Amount REAL NOT NULL, 
                    DepositorName TEXT,
                    Year INTEGER NOT NULL, 
                    Notes TEXT
                );
                
                -- جدول المساعدات
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
                
                -- جدول السيارات
                CREATE TABLE IF NOT EXISTS Cars (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT, 
                    CarNumber TEXT NOT NULL UNIQUE
                );
                
                -- جدول فواتير الوقود
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

                // إنشاء Indexes لتحسين الأداء
                string indexesSql = @"
                -- Indexes لجدول الفواتير
                CREATE INDEX IF NOT EXISTS idx_invoices_year ON Invoices(Year);
                CREATE INDEX IF NOT EXISTS idx_invoices_date ON Invoices(Date);
                CREATE INDEX IF NOT EXISTS idx_invoices_year_date ON Invoices(Year, Date);
                
                -- Indexes لجدول سندات القبض
                CREATE INDEX IF NOT EXISTS idx_receiptvouchers_year ON ReceiptVouchers(Year);
                CREATE INDEX IF NOT EXISTS idx_receiptvouchers_date ON ReceiptVouchers(Date);
                CREATE INDEX IF NOT EXISTS idx_receiptvouchers_year_date ON ReceiptVouchers(Year, Date);
                
                -- Indexes لجدول الإيداعات
                CREATE INDEX IF NOT EXISTS idx_deposits_year ON Deposits(Year);
                CREATE INDEX IF NOT EXISTS idx_deposits_date ON Deposits(Date);
                CREATE INDEX IF NOT EXISTS idx_deposits_year_date ON Deposits(Year, Date);
                
                -- Indexes لجدول المساعدات
                CREATE INDEX IF NOT EXISTS idx_aids_year ON Aids(Year);
                CREATE INDEX IF NOT EXISTS idx_aids_date ON Aids(Date);
                CREATE INDEX IF NOT EXISTS idx_aids_project ON Aids(ProjectName);
                CREATE INDEX IF NOT EXISTS idx_aids_year_project ON Aids(Year, ProjectName);
                
                -- Indexes لجدول فواتير الوقود
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
