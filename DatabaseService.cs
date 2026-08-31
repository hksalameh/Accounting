using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;

namespace AccountingApp
{
    /// <summary>
    /// فئة مركزية للتعامل مع قاعدة البيانات.
    /// تحافظ على التوافق مع ملفات invoices.db القديمة وتطبق ترقيات غير مدمرة فقط.
    /// </summary>
    public static class DatabaseService
    {
        private const string DatabaseFileName = "invoices.db";
        private const int CurrentSchemaVersion = 1;
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

        public static string BackupDirectory
        {
            get
            {
                var databaseDirectory = Path.GetDirectoryName(DatabasePath);
                if (string.IsNullOrWhiteSpace(databaseDirectory))
                {
                    databaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                }
                return Path.Combine(databaseDirectory, "Backups");
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

        public static SqliteConnection GetConnection()
        {
            return new SqliteConnection(ConnectionString);
        }

        /// <summary>
        /// ينشئ نسخة احتياطية متسقة باستخدام آلية SQLite الرسمية للنسخ أثناء التشغيل.
        /// يعيد مسار النسخة أو null إذا لم توجد قاعدة بيانات بعد.
        /// </summary>
        public static string CreateBackup(string reason = "manual")
        {
            if (!File.Exists(DatabasePath)) return null;

            Directory.CreateDirectory(BackupDirectory);
            string safeReason = SanitizeFilePart(reason);
            string fileName = $"invoices-{DateTime.Now:yyyyMMdd-HHmmss}-{safeReason}.db";
            string destination = Path.Combine(BackupDirectory, fileName);

            using (var sourceConnection = GetConnection())
            using (var destinationConnection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = destination }.ToString()))
            {
                sourceConnection.Open();
                sourceConnection.BackupDatabase(destinationConnection);
            }

            CleanupOldBackups(30);
            return destination;
        }

        /// <summary>
        /// ينشئ نسخة واحدة فقط في اليوم عند تشغيل البرنامج.
        /// </summary>
        public static string CreateDailyBackup()
        {
            if (!File.Exists(DatabasePath)) return null;

            Directory.CreateDirectory(BackupDirectory);
            string pattern = $"invoices-{DateTime.Now:yyyyMMdd}-*-daily.db";
            string existing = Directory.GetFiles(BackupDirectory, pattern)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            return existing ?? CreateBackup("daily");
        }

        /// <summary>
        /// يتحقق من أن الملف نسخة قاعدة بيانات خاصة بالبرنامج قبل السماح باستعادته.
        /// يدعم النسخ القديمة التي لا تحتوي بعد على VoucherNo.
        /// </summary>
        public static bool IsValidAccountingDatabase(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;

            try
            {
                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = filePath,
                    Mode = SqliteOpenMode.ReadOnly
                }.ToString();

                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();
                    string[] requiredTables =
                    {
                        "Invoices", "OpeningBalances", "ReceiptVouchers", "Deposits", "Aids"
                    };

                    foreach (string table in requiredTables)
                    {
                        using (var cmd = new SqliteCommand(
                            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@Name", conn))
                        {
                            cmd.Parameters.AddWithValue("@Name", table);
                            if (Convert.ToInt32(cmd.ExecuteScalar()) == 0) return false;
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// يستعيد نسخة قاعدة بيانات بعد التحقق منها، مع حفظ الوضع الحالي أولاً.
        /// إذا فشلت الترقية بعد الاستعادة يتم إرجاع النسخة السابقة تلقائياً.
        /// </summary>
        public static void RestoreBackup(string sourcePath)
        {
            if (!IsValidAccountingDatabase(sourcePath))
            {
                throw new InvalidOperationException("الملف المحدد ليس نسخة قاعدة بيانات صالحة لهذا البرنامج.");
            }

            string sourceFullPath = Path.GetFullPath(sourcePath);
            string targetFullPath = Path.GetFullPath(DatabasePath);
            if (string.Equals(sourceFullPath, targetFullPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("الملف المحدد هو قاعدة البيانات الحالية نفسها.");
            }

            string rollbackBackup = CreateBackup("pre-restore");

            try
            {
                File.Copy(sourceFullPath, targetFullPath, true);
                _isInitialized = false;
                InitializeDatabase();
            }
            catch
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(rollbackBackup) && File.Exists(rollbackBackup))
                    {
                        File.Copy(rollbackBackup, targetFullPath, true);
                        _isInitialized = false;
                        InitializeDatabase();
                    }
                }
                catch
                {
                    _isInitialized = false;
                }

                throw;
            }
        }

        private static string SanitizeFilePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "backup";
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '-');
            }
            return value.Trim().Replace(' ', '-');
        }

        private static void CleanupOldBackups(int keepCount)
        {
            try
            {
                if (!Directory.Exists(BackupDirectory)) return;
                var files = new DirectoryInfo(BackupDirectory)
                    .GetFiles("invoices-*.db")
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .Skip(keepCount)
                    .ToList();

                foreach (var file in files)
                {
                    try { file.Delete(); } catch { }
                }
            }
            catch
            {
                // فشل تنظيف نسخة قديمة لا يجب أن يمنع البرنامج من العمل.
            }
        }

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

        public static void InitializeDatabase()
        {
            if (_isInitialized) return;

            bool databaseExistedBeforeStartup = File.Exists(DatabasePath);

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
                    Year INTEGER NOT NULL,
                    VoucherNo TEXT
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
            }

            ApplyMigrations(databaseExistedBeforeStartup);
            CreateIndexes();
            _isInitialized = true;
        }

        private static void ApplyMigrations(bool databaseExistedBeforeStartup)
        {
            int version;
            bool hasVoucherNo;

            using (var conn = GetConnection())
            {
                conn.Open();
                version = GetUserVersion(conn);
                hasVoucherNo = ColumnExists(conn, "Aids", "VoucherNo");
            }

            if (version >= CurrentSchemaVersion && hasVoucherNo) return;

            if (databaseExistedBeforeStartup && !hasVoucherNo)
            {
                try
                {
                    CreateBackup("pre-migration-v1");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "تعذر إنشاء نسخة احتياطية قبل ترقية قاعدة البيانات. لم يتم إجراء أي تعديل على قاعدة البيانات.",
                        ex);
                }
            }

            int targetVersion = Math.Max(version, CurrentSchemaVersion);

            using (var conn = GetConnection())
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        if (!ColumnExists(conn, "Aids", "VoucherNo", transaction))
                        {
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.Transaction = transaction;
                                cmd.CommandText = "ALTER TABLE Aids ADD COLUMN VoucherNo TEXT;";
                                cmd.ExecuteNonQuery();
                            }
                        }

                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = $"PRAGMA user_version = {targetVersion};";
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private static int GetUserVersion(SqliteConnection conn)
        {
            using (var cmd = new SqliteCommand("PRAGMA user_version;", conn))
            {
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private static bool ColumnExists(SqliteConnection conn, string tableName, string columnName, SqliteTransaction transaction = null)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = $"PRAGMA table_info({tableName});";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static void CreateIndexes()
        {
            using (var conn = GetConnection())
            {
                conn.Open();
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
                CREATE INDEX IF NOT EXISTS idx_aids_voucher ON Aids(VoucherNo);

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
        }
    }
}
