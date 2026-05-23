using System;
using System.Globalization;
using System.Windows.Data;

namespace PinNote.UI
{
    public class BooleanToPinTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool isPinned && isPinned ? "Pinned" : "Pin";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
