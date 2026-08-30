using Microsoft.Data.Sqlite;
using System;

namespace AccountingApp
{
    public static class DashboardManager
    {
        private static readonly string _connectionString = DatabaseService.ConnectionString;

        private static decimal ExecuteScalarDecimal(string sql, int year)
        {
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Year", year);
                        var result = cmd.ExecuteScalar();
                        if (result != DBNull.Value && result != null)
                        {
                            return Convert.ToDecimal(result);
                        }
                    }
                }
            }
            catch (SqliteException) { return 0; }
            return 0;
        }

        public static decimal GetRevenuesBalance(int year)
        {
            string receiptsSql = "SELECT SUM(Amount) FROM ReceiptVouchers WHERE Year = @Year";
            string depositsSql = "SELECT SUM(Amount) FROM Deposits WHERE Year = @Year";

            decimal totalReceipts = ExecuteScalarDecimal(receiptsSql, year);
            decimal totalDeposits = ExecuteScalarDecimal(depositsSql, year);

            return totalReceipts - totalDeposits;
        }

        public static decimal GetInvoicesBalance(int year)
        {
            decimal openingBalance = 0;
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    var cmd = new SqliteCommand("SELECT Balance FROM OpeningBalances WHERE Year = @Year", conn);
                    cmd.Parameters.AddWithValue("@Year", year);
                    var result = cmd.ExecuteScalar();
                    if (result != null) decimal.TryParse(result.ToString(), out openingBalance);
                }
            }
            catch (SqliteException) { openingBalance = 0; }

            string debitSql = "SELECT SUM(Debit) FROM Invoices WHERE Year = @Year";
            string creditSql = "SELECT SUM(Credit) FROM Invoices WHERE Year = @Year";

            decimal totalDebit = ExecuteScalarDecimal(debitSql, year);
            decimal totalCredit = ExecuteScalarDecimal(creditSql, year);

            return openingBalance + totalDebit - totalCredit;
        }
    }
}