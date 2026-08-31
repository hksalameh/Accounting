using Microsoft.Data.Sqlite;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace AccountingApp
{
    public static class ReportExportService
    {
        private const string SpreadsheetNs = "urn:schemas-microsoft-com:office:spreadsheet";
        private const string OfficeNs = "urn:schemas-microsoft-com:office:office";
        private const string ExcelNs = "urn:schemas-microsoft-com:office:excel";

        public static void ExportYearToExcelXml(int year, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("مسار ملف التصدير غير صحيح.", nameof(filePath));
            }

            if (!DashboardManager.TryGetSnapshot(year, out DashboardSnapshot snapshot, out string errorMessage))
            {
                throw new InvalidOperationException("تعذر قراءة بيانات السنة للتصدير. " + errorMessage);
            }

            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                CloseOutput = true
            };

            using (var writer = XmlWriter.Create(filePath, settings))
            {
                writer.WriteStartDocument();
                writer.WriteProcessingInstruction("mso-application", "progid=\"Excel.Sheet\"");
                writer.WriteStartElement("Workbook", SpreadsheetNs);
                writer.WriteAttributeString("xmlns", "o", null, OfficeNs);
                writer.WriteAttributeString("xmlns", "x", null, ExcelNs);
                writer.WriteAttributeString("xmlns", "ss", null, SpreadsheetNs);

                WriteStyles(writer);
                WriteSummarySheet(writer, snapshot);

                using (var conn = DatabaseService.GetConnection())
                {
                    conn.Open();
                    WriteInvoicesSheet(writer, conn, year);
                    WriteReceiptsSheet(writer, conn, year);
                    WriteDepositsSheet(writer, conn, year);
                    WriteAidsSheet(writer, conn, year);
                    WriteFuelSheet(writer, conn, year);
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void WriteStyles(XmlWriter writer)
        {
            writer.WriteStartElement("Styles", SpreadsheetNs);

            writer.WriteStartElement("Style", SpreadsheetNs);
            writer.WriteAttributeString("ss", "ID", SpreadsheetNs, "Header");
            writer.WriteStartElement("Font", SpreadsheetNs);
            writer.WriteAttributeString("ss", "Bold", SpreadsheetNs, "1");
            writer.WriteEndElement();
            writer.WriteStartElement("Interior", SpreadsheetNs);
            writer.WriteAttributeString("ss", "Color", SpreadsheetNs, "#E2E8F0");
            writer.WriteAttributeString("ss", "Pattern", SpreadsheetNs, "Solid");
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteStartElement("Style", SpreadsheetNs);
            writer.WriteAttributeString("ss", "ID", SpreadsheetNs, "Money");
            writer.WriteStartElement("NumberFormat", SpreadsheetNs);
            writer.WriteAttributeString("ss", "Format", SpreadsheetNs, "0.000");
            writer.WriteEndElement();
            writer.WriteEndElement();

            writer.WriteEndElement();
        }

        private static void WriteSummarySheet(XmlWriter writer, DashboardSnapshot snapshot)
        {
            StartSheet(writer, "الملخص");
            WriteHeaderRow(writer, "البيان", "القيمة");
            WriteTextNumberRow(writer, "السنة المالية", snapshot.Year);
            WriteTextNumberRow(writer, "إجمالي سندات القبض", snapshot.TotalReceipts, true);
            WriteTextNumberRow(writer, "إجمالي الإيداعات", snapshot.TotalDeposits, true);
            WriteTextNumberRow(writer, "رصيد الإيرادات", snapshot.RevenuesBalance, true);
            WriteTextNumberRow(writer, "الرصيد الافتتاحي لصندوق الفواتير", snapshot.OpeningBalance, true);
            WriteTextNumberRow(writer, "تغذية صندوق الفواتير", snapshot.FundAdditions, true);
            WriteTextNumberRow(writer, "صرف الفواتير", snapshot.InvoiceExpenses, true);
            WriteTextNumberRow(writer, "رصيد صندوق الفواتير", snapshot.InvoicesBalance, true);
            WriteTextNumberRow(writer, "رصيد الصندوق", snapshot.FundBalance, true);
            WriteTextNumberRow(writer, "إجمالي مبالغ المساعدات", snapshot.TotalAids, true);
            WriteTextNumberRow(writer, "إجمالي فواتير الوقود", snapshot.TotalFuel, true);
            WriteTextNumberRow(writer, "الوقود غير المدفوع", snapshot.UnpaidFuel, true);
            EndSheet(writer);
        }

        private static void WriteInvoicesSheet(XmlWriter writer, SqliteConnection conn, int year)
        {
            StartSheet(writer, "صندوق الفواتير");
            WriteHeaderRow(writer, "التاريخ", "رقم الفاتورة/المرجع", "البيان", "تغذية الصندوق", "صرف فاتورة");

            using (var cmd = new SqliteCommand(@"
SELECT Date, InvoiceNo, Description, COALESCE(Debit,0), COALESCE(Credit,0)
FROM Invoices WHERE Year=@Year ORDER BY date(Date), Id", conn))
            {
                cmd.Parameters.AddWithValue("@Year", year);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        StartRow(writer);
                        WriteCell(writer, FormatDate(reader.GetString(0)));
                        WriteCell(writer, reader.IsDBNull(1) ? string.Empty : reader.GetString(1));
                        WriteCell(writer, reader.IsDBNull(2) ? string.Empty : reader.GetString(2));
                        WriteNumberCell(writer, Convert.ToDecimal(reader.GetValue(3)), true);
                        WriteNumberCell(writer, Convert.ToDecimal(reader.GetValue(4)), true);
                        EndRow(writer);
                    }
                }
            }
            EndSheet(writer);
        }

        private static void WriteReceiptsSheet(XmlWriter writer, SqliteConnection conn, int year)
        {
            StartSheet(writer, "سندات القبض");
            WriteHeaderRow(writer, "التاريخ", "رقم الدفتر", "من سند", "إلى سند", "المستلم", "المبلغ", "ملاحظات");

            using (var cmd = new SqliteCommand(@"
SELECT Date, BookNo, FromVoucherNo, ToVoucherNo, Recipient, Amount, Notes
FROM ReceiptVouchers WHERE Year=@Year ORDER BY date(Date), Id", conn))
            {
                cmd.Parameters.AddWithValue("@Year", year);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        StartRow(writer);
                        WriteCell(writer, FormatDate(reader.GetString(0)));
                        WriteCell(writer, reader.IsDBNull(1) ? string.Empty : reader.GetString(1));
                        WriteCell(writer, reader.IsDBNull(2) ? string.Empty : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture));
                        WriteCell(writer, reader.IsDBNull(3) ? string.Empty : Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture));
                        WriteCell(writer, reader.IsDBNull(4) ? string.Empty : reader.GetString(4));
                        WriteNumberCell(writer, reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetValue(5)), true);
                        WriteCell(writer, reader.IsDBNull(6) ? string.Empty : reader.GetString(6));
                        EndRow(writer);
                    }
                }
            }
            EndSheet(writer);
        }

        private static void WriteDepositsSheet(XmlWriter writer, SqliteConnection conn, int year)
        {
            StartSheet(writer, "الإيداعات");
            WriteHeaderRow(writer, "التاريخ", "اسم المودع", "المبلغ", "ملاحظات");

            using (var cmd = new SqliteCommand(@"
SELECT Date, DepositorName, Amount, Notes
FROM Deposits WHERE Year=@Year ORDER BY date(Date), Id", conn))
            {
                cmd.Parameters.AddWithValue("@Year", year);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        StartRow(writer);
                        WriteCell(writer, FormatDate(reader.GetString(0)));
                        WriteCell(writer, reader.IsDBNull(1) ? string.Empty : reader.GetString(1));
                        WriteNumberCell(writer, reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2)), true);
                        WriteCell(writer, reader.IsDBNull(3) ? string.Empty : reader.GetString(3));
                        EndRow(writer);
                    }
                }
            }
            EndSheet(writer);
        }

        private static void WriteAidsSheet(XmlWriter writer, SqliteConnection conn, int year)
        {
            StartSheet(writer, "المساعدات");
            WriteHeaderRow(writer, "المشروع", "رقم السند", "التاريخ", "المتبرع", "البيان/النوع", "الكمية", "المبلغ");

            using (var cmd = new SqliteCommand(@"
SELECT ProjectName, VoucherNo, Date, DonorName, DonationType, Quantity, Amount
FROM Aids WHERE Year=@Year ORDER BY ProjectName, date(Date), Id", conn))
            {
                cmd.Parameters.AddWithValue("@Year", year);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        StartRow(writer);
                        WriteCell(writer, reader.GetString(0));
                        WriteCell(writer, reader.IsDBNull(1) ? string.Empty : reader.GetString(1));
                        WriteCell(writer, FormatDate(reader.GetString(2)));
                        WriteCell(writer, reader.IsDBNull(3) ? string.Empty : reader.GetString(3));
                        WriteCell(writer, reader.IsDBNull(4) ? string.Empty : reader.GetString(4));
                        WriteNumberCell(writer, reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetValue(5)), false);
                        WriteNumberCell(writer, reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader.GetValue(6)), true);
                        EndRow(writer);
                    }
                }
            }
            EndSheet(writer);
        }

        private static void WriteFuelSheet(XmlWriter writer, SqliteConnection conn, int year)
        {
            StartSheet(writer, "الوقود");
            WriteHeaderRow(writer, "رقم السيارة", "رقم الفاتورة", "التاريخ", "المبلغ", "حالة الدفع");

            using (var cmd = new SqliteCommand(@"
SELECT CarNumber, InvoiceNumber, Date, Amount, IsPaid
FROM FuelInvoices WHERE Year=@Year ORDER BY CarNumber, date(Date), Id", conn))
            {
                cmd.Parameters.AddWithValue("@Year", year);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        StartRow(writer);
                        WriteCell(writer, reader.GetString(0));
                        WriteCell(writer, reader.IsDBNull(1) ? string.Empty : reader.GetString(1));
                        WriteCell(writer, FormatDate(reader.GetString(2)));
                        WriteNumberCell(writer, reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3)), true);
                        WriteCell(writer, !reader.IsDBNull(4) && Convert.ToInt32(reader.GetValue(4)) == 1 ? "مدفوع" : "غير مدفوع");
                        EndRow(writer);
                    }
                }
            }
            EndSheet(writer);
        }

        private static string FormatDate(string value)
        {
            if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date) ||
                DateTime.TryParse(value, out date))
            {
                return date.ToString(AppSettings.DateFormat);
            }
            return value ?? string.Empty;
        }

        private static void StartSheet(XmlWriter writer, string name)
        {
            writer.WriteStartElement("Worksheet", SpreadsheetNs);
            writer.WriteAttributeString("ss", "Name", SpreadsheetNs, name);
            writer.WriteStartElement("Table", SpreadsheetNs);
        }

        private static void EndSheet(XmlWriter writer)
        {
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteHeaderRow(XmlWriter writer, params string[] values)
        {
            StartRow(writer);
            foreach (string value in values)
            {
                writer.WriteStartElement("Cell", SpreadsheetNs);
                writer.WriteAttributeString("ss", "StyleID", SpreadsheetNs, "Header");
                WriteData(writer, value, "String");
                writer.WriteEndElement();
            }
            EndRow(writer);
        }

        private static void WriteTextNumberRow(XmlWriter writer, string label, decimal value, bool money = false)
        {
            StartRow(writer);
            WriteCell(writer, label);
            WriteNumberCell(writer, value, money);
            EndRow(writer);
        }

        private static void WriteTextNumberRow(XmlWriter writer, string label, int value)
        {
            StartRow(writer);
            WriteCell(writer, label);
            WriteNumberCell(writer, value, false);
            EndRow(writer);
        }

        private static void StartRow(XmlWriter writer)
        {
            writer.WriteStartElement("Row", SpreadsheetNs);
        }

        private static void EndRow(XmlWriter writer)
        {
            writer.WriteEndElement();
        }

        private static void WriteCell(XmlWriter writer, string value)
        {
            writer.WriteStartElement("Cell", SpreadsheetNs);
            WriteData(writer, value ?? string.Empty, "String");
            writer.WriteEndElement();
        }

        private static void WriteNumberCell(XmlWriter writer, decimal value, bool money)
        {
            writer.WriteStartElement("Cell", SpreadsheetNs);
            if (money)
            {
                writer.WriteAttributeString("ss", "StyleID", SpreadsheetNs, "Money");
            }
            WriteData(writer, value.ToString(CultureInfo.InvariantCulture), "Number");
            writer.WriteEndElement();
        }

        private static void WriteData(XmlWriter writer, string value, string type)
        {
            writer.WriteStartElement("Data", SpreadsheetNs);
            writer.WriteAttributeString("ss", "Type", SpreadsheetNs, type);
            writer.WriteString(value ?? string.Empty);
            writer.WriteEndElement();
        }
    }
}
