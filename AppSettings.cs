namespace AccountingApp
{
    public static class AppSettings
    {
        // هذا هو المكان الوحيد الذي ستغيره في المستقبل لتعديل صيغة التاريخ
        // أمثلة:
        // "d/M/yyyy" -> 2/11/2025
        // "dd/MM/yyyy" -> 02/11/2025
        // "yyyy-MM-dd" -> 2025-11-02
        public const string DateFormat = "yyyy/MM/dd";
    }
}