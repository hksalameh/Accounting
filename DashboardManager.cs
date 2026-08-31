using Microsoft.Data.Sqlite;
using System;

namespace AccountingApp
{
    public static class DashboardManager
    {
        private static readonly string _connectionString = DatabaseService.ConnectionString;

        /// <summary>
        /// Reads all dashboard balances for one financial year as one operation.
        /// If any database read fails, no financial value is returned as a misleading zero.
        /// </summary>
        public static bool TryGetBalances(
            int year,
            out decimal revenuesBalance,
            out decimal invoicesBalance,
            out string errorMessage)
        {
            revenuesBalance = 0;
            invoicesBalance = 0;
            errorMessage = null;

            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();

                    decimal totalReceipts = ExecuteScalarDecimal(
                        conn,
                        "SELECT COALESCE(SUM(Amount), 0) FROM ReceiptVouchers WHERE Year = @Year",
                        year);

                    decimal totalDeposits = ExecuteScalarDecimal(
                        conn,
                        "SELECT COALESCE(SUM(Amount), 0) FROM Deposits WHERE Year = @Year",
                        year);

                    decimal openingBalance = ExecuteScalarDecimal(
                        conn,
                        "SELECT COALESCE(Balance, 0) FROM OpeningBalances WHERE Year = @Year",
                        year);

                    decimal totalFundAdditions = ExecuteScalarDecimal(
                        conn,
                        "SELECT COALESCE(SUM(Debit), 0) FROM Invoices WHERE Year = @Year",
                        year);

                    decimal totalInvoiceExpenses = ExecuteScalarDecimal(
                        conn,
                        "SELECT COALESCE(SUM(Credit), 0) FROM Invoices WHERE Year = @Year",
                        year);

                    // الإيرادات مستقلة لكل سنة مالية.
                    revenuesBalance = totalReceipts - totalDeposits;

                    // صندوق الفواتير: الرصيد الافتتاحي + تغذيات الصندوق - الفواتير المصروفة.
                    invoicesBalance = openingBalance + totalFundAdditions - totalInvoiceExpenses;
                }

                return true;
            }
            catch (Exception ex)
            {
                // لا نحول خطأ قاعدة البيانات إلى رصيد صفر؛ نعيد فشل واضح للواجهة.
                errorMessage = ex.Message;
                revenuesBalance = 0;
                invoicesBalance = 0;
                return false;
            }
        }

        private static decimal ExecuteScalarDecimal(SqliteConnection conn, string sql, int year)
        {
            using (var cmd = new SqliteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@Year", year);
                var result = cmd.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                {
                    return 0;
                }

                return Convert.ToDecimal(result);
            }
        }

        // نبقي الدوال القديمة للتوافق مع أي استدعاءات حالية داخل المشروع،
        // لكن لا نخفي أخطاء قاعدة البيانات بعد الآن.
        public static decimal GetRevenuesBalance(int year)
        {
            if (!TryGetBalances(year, out decimal revenuesBalance, out _, out string errorMessage))
            {
                throw new InvalidOperationException("تعذر قراءة رصيد الإيرادات من قاعدة البيانات.", new Exception(errorMessage));
            }

            return revenuesBalance;
        }

        public static decimal GetInvoicesBalance(int year)
        {
            if (!TryGetBalances(year, out _, out decimal invoicesBalance, out string errorMessage))
            {
                throw new InvalidOperationException("تعذر قراءة رصيد صندوق الفواتير من قاعدة البيانات.", new Exception(errorMessage));
            }

            return invoicesBalance;
        }
    }
}
