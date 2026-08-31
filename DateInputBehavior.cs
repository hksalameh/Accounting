using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AccountingApp
{
    /// <summary>
    /// سلوك موحد لحقول التاريخ في جميع الشاشات بدون تغيير طريقة تخزين التاريخ.
    /// يسمح بكتابة 31/8 أو 31-8 ثم يحولها إلى الصيغة الموحدة عند مغادرة الحقل.
    /// </summary>
    public static class DateInputBehavior
    {
        private static bool _enabled;

        public static void Enable()
        {
            if (_enabled) return;
            _enabled = true;

            EventManager.RegisterClassHandler(
                typeof(TextBox),
                Keyboard.GotKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler(OnGotKeyboardFocus),
                true);

            EventManager.RegisterClassHandler(
                typeof(TextBox),
                Keyboard.LostKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler(OnLostKeyboardFocus),
                true);
        }

        private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (!IsDateTextBox(textBox)) return;

            if (textBox.ToolTip == null)
            {
                textBox.ToolTip = "يمكن كتابة التاريخ كاملًا أو كتابة اليوم والشهر فقط، مثل 31/8.";
            }

            textBox.Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(textBox.SelectAll));
        }

        private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (!IsDateTextBox(textBox) || string.IsNullOrWhiteSpace(textBox.Text)) return;

            int selectedYear = FindSelectedFiscalYear(textBox) ?? DateTime.Now.Year;
            DateTime parsedDate;
            if (DatabaseService.TryParseDate(textBox.Text, out parsedDate, false, selectedYear))
            {
                textBox.Text = parsedDate.ToString(AppSettings.DateFormat, CultureInfo.InvariantCulture);
            }
        }

        private static bool IsDateTextBox(TextBox textBox)
        {
            if (textBox == null || string.IsNullOrWhiteSpace(textBox.Name)) return false;
            return textBox.Name.IndexOf("DateTextBox", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int? FindSelectedFiscalYear(DependencyObject start)
        {
            DependencyObject current = start;
            while (current != null)
            {
                var userControl = current as UserControl;
                if (userControl != null)
                {
                    var yearComboBox = userControl.FindName("YearComboBox") as ComboBox;
                    int year;
                    if (yearComboBox != null && yearComboBox.SelectedItem != null &&
                        int.TryParse(yearComboBox.SelectedItem.ToString(), out year))
                    {
                        return year;
                    }
                    break;
                }

                current = GetParent(current);
            }

            return null;
        }

        private static DependencyObject GetParent(DependencyObject child)
        {
            if (child == null) return null;

            var parent = VisualTreeHelper.GetParent(child);
            if (parent != null) return parent;

            var frameworkElement = child as FrameworkElement;
            return frameworkElement?.Parent;
        }
    }
}
