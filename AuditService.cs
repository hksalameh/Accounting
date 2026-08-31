using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace AccountingApp
{
    public sealed class AuditLogEntry
    {
        public long Id { get; set; }
        public string EventTime { get; set; }
        public string Action { get; set; }
        public string TableName { get; set; }
        public string EntityName => GetEntityName(TableName);
        public long RecordId { get; set; }
        public string Details { get; set; }
        public string UserName { get; set; }
        public string MachineName { get; set; }

        private static string GetEntityName(string tableName)
        {
            switch (tableName)
            {
                case "Invoices": return "صندوق الفواتير";
                case "OpeningBalances": return "الرصيد الافتتاحي";
                case "ReceiptVouchers": return "سندات القبض";
                case "Deposits": return "الإيداعات";
                case "Aids": return "المساعدات";
                case "Cars": return "السيارات";
                case "FuelInvoices": return "فواتير الوقود";
                default: return tableName ?? string.Empty;
            }
        }
    }

    public static class AuditService
    {
        public static void Initialize()
        {
            using (var conn = DatabaseService.GetConnection())
            {
                conn.Open();

                const string schemaSql = @"
CREATE TABLE IF NOT EXISTS AuditLog (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EventTime TEXT NOT NULL,
    Action TEXT NOT NULL,
    TableName TEXT NOT NULL,
    RecordId INTEGER NOT NULL,
    Details TEXT,
    UserName TEXT,
    MachineName TEXT
);
CREATE INDEX IF NOT EXISTS idx_audit_eventtime ON AuditLog(EventTime);
CREATE INDEX IF NOT EXISTS idx_audit_table ON AuditLog(TableName);

CREATE TABLE IF NOT EXISTS AppSession (
    Id INTEGER PRIMARY KEY CHECK (Id = 1),
    UserName TEXT,
    MachineName TEXT
);";

                using (var cmd = new SqliteCommand(schemaSql, conn))
                {
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new SqliteCommand(
                    "INSERT OR REPLACE INTO AppSession (Id, UserName, MachineName) VALUES (1, @UserName, @MachineName)", conn))
                {
                    cmd.Parameters.AddWithValue("@UserName", Environment.UserName ?? string.Empty);
                    cmd.Parameters.AddWithValue("@MachineName", Environment.MachineName ?? string.Empty);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new SqliteCommand(GetTriggerSql(), conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static List<AuditLogEntry> LoadRecent(int maxRows = 500)
        {
            if (maxRows < 1) maxRows = 1;
            if (maxRows > 5000) maxRows = 5000;

            var result = new List<AuditLogEntry>();
            using (var conn = DatabaseService.GetConnection())
            {
                conn.Open();
                using (var cmd = new SqliteCommand(@"
SELECT Id, EventTime, Action, TableName, RecordId, Details, UserName, MachineName
FROM AuditLog
ORDER BY Id DESC
LIMIT @Limit", conn))
                {
                    cmd.Parameters.AddWithValue("@Limit", maxRows);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(new AuditLogEntry
                            {
                                Id = reader.GetInt64(0),
                                EventTime = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                Action = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                TableName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                RecordId = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                                Details = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                                UserName = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                                MachineName = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
                            });
                        }
                    }
                }
            }
            return result;
        }

        private static string GetTriggerSql()
        {
            return @"
CREATE TRIGGER IF NOT EXISTS trg_invoices_insert AFTER INSERT ON Invoices BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'إضافة','Invoices',NEW.Id,
    'رقم: ' || COALESCE(NEW.InvoiceNo,'') || ' | بيان: ' || COALESCE(NEW.Description,''),
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;
CREATE TRIGGER IF NOT EXISTS trg_invoices_update AFTER UPDATE ON Invoices BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'تعديل','Invoices',NEW.Id,
    'رقم: ' || COALESCE(NEW.InvoiceNo,'') || ' | بيان: ' || COALESCE(NEW.Description,''),
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;
CREATE TRIGGER IF NOT EXISTS trg_invoices_delete AFTER DELETE ON Invoices BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'حذف','Invoices',OLD.Id,
    'رقم: ' || COALESCE(OLD.InvoiceNo,'') || ' | بيان: ' || COALESCE(OLD.Description,''),
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;

CREATE TRIGGER IF NOT EXISTS trg_opening_insert AFTER INSERT ON OpeningBalances BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'إضافة','OpeningBalances',NEW.Year,
    'السنة: ' || NEW.Year || ' | الرصيد: ' || NEW.Balance,
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;
CREATE TRIGGER IF NOT EXISTS trg_opening_update AFTER UPDATE ON OpeningBalances BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'تعديل','OpeningBalances',NEW.Year,
    'السنة: ' || NEW.Year || ' | الرصيد: ' || NEW.Balance,
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;
CREATE TRIGGER IF NOT EXISTS trg_opening_delete AFTER DELETE ON OpeningBalances BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'حذف','OpeningBalances',OLD.Year,
    'السنة: ' || OLD.Year || ' | الرصيد: ' || OLD.Balance,
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;

CREATE TRIGGER IF NOT EXISTS trg_receipts_insert AFTER INSERT ON ReceiptVouchers BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'إضافة','ReceiptVouchers',NEW.Id,
    'الدفتر: ' || COALESCE(NEW.BookNo,'') || ' | المستلم: ' || COALESCE(NEW.Recipient,'') || ' | المبلغ: ' || NEW.Amount,
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;
CREATE TRIGGER IF NOT EXISTS trg_receipts_update AFTER UPDATE ON ReceiptVouchers BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'تعديل','ReceiptVouchers',NEW.Id,
    'الدفتر: ' || COALESCE(NEW.BookNo,'') || ' | المستلم: ' || COALESCE(NEW.Recipient,'') || ' | المبلغ: ' || NEW.Amount,
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;
CREATE TRIGGER IF NOT EXISTS trg_receipts_delete AFTER DELETE ON ReceiptVouchers BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'حذف','ReceiptVouchers',OLD.Id,
    'الدفتر: ' || COALESCE(OLD.BookNo,'') || ' | المستلم: ' || COALESCE(OLD.Recipient,'') || ' | المبلغ: ' || OLD.Amount,
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;

CREATE TRIGGER IF NOT EXISTS trg_deposits_insert AFTER INSERT ON Deposits BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'إضافة','Deposits',NEW.Id,
    'المودع: ' || COALESCE(NEW.DepositorName,'') || ' | المبلغ: ' || NEW.Amount,
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;
CREATE TRIGGER IF NOT EXISTS trg_deposits_update AFTER UPDATE ON Deposits BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'تعديل','Deposits',NEW.Id,
    'المودع: ' || COALESCE(NEW.DepositorName,'') || ' | المبلغ: ' || NEW.Amount,
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;
CREATE TRIGGER IF NOT EXISTS trg_deposits_delete AFTER DELETE ON Deposits BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'حذف','Deposits',OLD.Id,
    'المودع: ' || COALESCE(OLD.DepositorName,'') || ' | المبلغ: ' || OLD.Amount,
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;

CREATE TRIGGER IF NOT EXISTS trg_aids_insert AFTER INSERT ON Aids BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'إضافة','Aids',NEW.Id,
    'المشروع: ' || NEW.ProjectName || ' | السند: ' || COALESCE(NEW.VoucherNo,'') || ' | المتبرع: ' || COALESCE(NEW.DonorName,''),
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;
CREATE TRIGGER IF NOT EXISTS trg_aids_update AFTER UPDATE ON Aids BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'تعديل','Aids',NEW.Id,
    'المشروع: ' || NEW.ProjectName || ' | السند: ' || COALESCE(NEW.VoucherNo,'') || ' | المتبرع: ' || COALESCE(NEW.DonorName,''),
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;
CREATE TRIGGER IF NOT EXISTS trg_aids_delete AFTER DELETE ON Aids BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'حذف','Aids',OLD.Id,
    'المشروع: ' || OLD.ProjectName || ' | السند: ' || COALESCE(OLD.VoucherNo,'') || ' | المتبرع: ' || COALESCE(OLD.DonorName,''),
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;

CREATE TRIGGER IF NOT EXISTS trg_cars_insert AFTER INSERT ON Cars BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'إضافة','Cars',NEW.Id,
    'رقم السيارة: ' || NEW.CarNumber,
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;
CREATE TRIGGER IF NOT EXISTS trg_cars_delete AFTER DELETE ON Cars BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'حذف','Cars',OLD.Id,
    'رقم السيارة: ' || OLD.CarNumber,
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;

CREATE TRIGGER IF NOT EXISTS trg_fuel_insert AFTER INSERT ON FuelInvoices BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'إضافة','FuelInvoices',NEW.Id,
    'السيارة: ' || NEW.CarNumber || ' | الفاتورة: ' || COALESCE(NEW.InvoiceNumber,'') || ' | المبلغ: ' || NEW.Amount,
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;
CREATE TRIGGER IF NOT EXISTS trg_fuel_update AFTER UPDATE ON FuelInvoices BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'تعديل','FuelInvoices',NEW.Id,
    'السيارة: ' || NEW.CarNumber || ' | الفاتورة: ' || COALESCE(NEW.InvoiceNumber,'') || ' | المبلغ: ' || NEW.Amount,
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;
CREATE TRIGGER IF NOT EXISTS trg_fuel_delete AFTER DELETE ON FuelInvoices BEGIN
  INSERT INTO AuditLog(EventTime,Action,TableName,RecordId,Details,UserName,MachineName)
  VALUES(strftime('%Y-%m-%d %H:%M:%S','now','localtime'),'حذف','FuelInvoices',OLD.Id,
    'السيارة: ' || OLD.CarNumber || ' | الفاتورة: ' || COALESCE(OLD.InvoiceNumber,'') || ' | المبلغ: ' || OLD.Amount,
    COALESCE((SELECT UserName FROM AppSession WHERE Id=1),''), COALESCE((SELECT MachineName FROM AppSession WHERE Id=1),''));
END;";
        }
    }
}
