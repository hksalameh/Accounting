using System;

namespace AccountingApp
{
    public class AidEntry
    {
        public int Id { get; set; }
        public string ProjectName { get; set; } // اسم المشروع (طرود غذائية, ملابس, ...)
        public string DonorName { get; set; }   // اسم المتبرع
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public int Quantity { get; set; }       // العدد (طرود, أسر, وجبات, ...)
        public string DonationType { get; set; } // نوع التبرع (خاص بمعونة الشتاء والطرود)
        public int Year { get; set; }
    }

    // هذا الكلاس الصغير سيستخدم لعرض ملخص المشاريع في الجدول العلوي
    public class ProjectSummary
    {
        public string ProjectName { get; set; }
        public decimal TotalAmount { get; set; }
        public int TotalQuantity { get; set; }
    }
}