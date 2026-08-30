using System;

namespace AccountingApp
{
    public class ReceiptVoucher
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string BookNo { get; set; }
        public int FromVoucherNo { get; set; }
        public int ToVoucherNo { get; set; }
        public string Recipient { get; set; }
        public decimal Amount { get; set; }
        public int Year { get; set; }
        public string Notes { get; set; }
    }
}