using System;

namespace AccountingApp
{
    public class Deposit
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string DepositorName { get; set; }
        public int Year { get; set; }
        public string Notes { get; set; }
    }
}