using System;
using System.Windows;
using System.Windows.Controls;

namespace AccountingApp
{
    public static class FiscalYearHelper
    {
        public const int MinYear = 2020;
        public const int MaxYear = 2035;
        public static int CurrentYear { get; private set; } = DateTime.Now.Year;

        public static void PopulateYears(ComboBox comboBox)
        {
            if (comboBox == null || comboBox.Items.Count > 0) return;

            for (int year = MinYear; year <= MaxYear; year++)
            {
                comboBox.Items.Add(year.ToString());
            }
        }

        public static void SelectCurrentYear(ComboBox comboBox)
        {
            if (comboBox == null) return;
            PopulateYears(comboBox);
            comboBox.SelectedItem = CurrentYear.ToString();
        }

        public static int GetSelectedYear(ComboBox comboBox)
        {
            if (comboBox != null && int.TryParse(comboBox.SelectedItem?.ToString(), out int year))
            {
                CurrentYear = year;
                return year;
            }

            return CurrentYear;
        }

        public static void ResetDateRange(ComboBox yearComboBox, TextBox fromDateTextBox, TextBox toDateTextBox)
        {
            int selectedYear = GetSelectedYear(yearComboBox);

            if (fromDateTextBox != null)
            {
                fromDateTextBox.Text = new DateTime(selectedYear, 1, 1).ToString(AppSettings.DateFormat);
            }

            if (toDateTextBox != null)
            {
                DateTime endDate = selectedYear == DateTime.Now.Year
                    ? DateTime.Now
                    : new DateTime(selectedYear, 12, 31);

                toDateTextBox.Text = endDate.ToString(AppSettings.DateFormat);
            }
        }

        public static bool ValidateDateInSelectedYear(DateTime date, ComboBox yearComboBox, string fieldName = "التاريخ")
        {
            int selectedYear = GetSelectedYear(yearComboBox);
            if (date.Year == selectedYear) return true;

            MessageBox.Show(
                $"{fieldName} يقع في سنة {date.Year} بينما السنة المالية المختارة هي {selectedYear}.\n\nغيّر السنة المالية أو صحح التاريخ قبل الحفظ.",
                "اختلاف السنة المالية",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return false;
        }
    }
}
