using Microsoft.Data.Sqlite;
using System;

namespace AccountingApp
{
    public sealed class DashboardSnapshot
    {
        public int Year { get; set; }
        public decimal TotalReceipts { get; set; }
        public decimal TotalDeposits { get; set; }
        public decimal RevenuesBalance { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal FundAdditions { get; set; }
        public decimal InvoiceExpenses { get; set; }
        public decimal InvoicesBalance { get; set; }
        public decimal TotalAids { get; set; }
        public decimal TotalFuel { get; set; }
        public decimal UnpaidFuel { get; set; }
        public decimal FundBalance => RevenuesBalance + InvoicesBalance;
    }

    public static class DashboardManager
    {
        private static readonly string _connectionString = DatabaseService.ConnectionString;

        public static bool TryGetSnapshot(int year, out DashboardSnapshot snapshot, out string errorMessage)
        {
            snapshot = null;
            errorMessage = null;

            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();

                    var result = new DashboardSnapshot
                    {
                        Year = year,
                        TotalReceipts = ExecuteScalarDecimal(
                            conn,
                            "SELECT COALESCE(SUM(Amount), 0) FROM ReceiptVouchers WHERE Year = @Year",
                            year),
                        TotalDeposits = ExecuteScalarDecimal(
                            conn,
                            "SELECT COALESCE(SUM(Amount), 0) FROM Deposits WHERE Year = @Year",
                            year),
                        OpeningBalance = ExecuteScalarDecimal(
                            conn,
                            "SELECT COALESCE(Balance, 0) FROM OpeningBalances WHERE Year = @Year",
                            year),
                        FundAdditions = ExecuteScalarDecimal(
                            conn,
                            "SELECT COALESCE(SUM(Debit), 0) FROM Invoices WHERE Year = @Year",
                            year),
                        InvoiceExpenses = ExecuteScalarDecimal(
                            conn,
                            "SELECT COALESCE(SUM(Credit), 0) FROM Invoices WHERE Year = @Year",
                            year),
                        TotalAids = ExecuteScalarDecimal(
                            conn,
                            "SELECT COALESCE(SUM(Amount), 0) FROM Aids WHERE Year = @Year",
                            year),
                        TotalFuel = ExecuteScalarDecimal(
                            conn,
                            "SELECT COALESCE(SUM(Amount), 0) FROM FuelInvoices WHERE Year = @Year",
                            year),
                        UnpaidFuel = ExecuteScalarDecimal(
                            conn,
                            "SELECT COALESCE(SUM(Amount), 0) FROM FuelInvoices WHERE Year = @Year AND IsPaid = 0",
                            year)
                    };

                    result.RevenuesBalance = result.TotalReceipts - result.TotalDeposits;
                    result.InvoicesBalance = result.OpeningBalance + result.FundAdditions - result.InvoiceExpenses;
                    snapshot = result;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                snapshot = null;
                return false;
            }
        }

        public static bool TryGetBalances(
            int year,
            out decimal revenuesBalance,
            out decimal invoicesBalance,
            out string errorMessage)
        {
            revenuesBalance = 0;
            invoicesBalance = 0;

            if (!TryGetSnapshot(year, out DashboardSnapshot snapshot, out errorMessage))
            {
                return false;
            }

            revenuesBalance = snapshot.RevenuesBalance;
            invoicesBalance = snapshot.InvoicesBalance;
            return true;
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
