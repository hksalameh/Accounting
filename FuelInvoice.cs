using System;

namespace AccountingApp
{
    public class FuelInvoice
    {
        public int Id { get; set; }
        public string CarNumber { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public bool IsPaid { get; set; }
        public int Year { get; set; }
        public decimal AccumulatedBalance { get; set; }
    }
}
