using System;

namespace AccountingApp
{
    public class AidEntry
    {
        public int Id { get; set; }
        public string ProjectName { get; set; }
        public string VoucherNo { get; set; }
        public string DonorName { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public int Quantity { get; set; }
        public string DonationType { get; set; }
        public int Year { get; set; }
    }

    public class ProjectSummary
    {
        public string ProjectName { get; set; }
        public decimal TotalAmount { get; set; }
        public int TotalQuantity { get; set; }
    }
}
