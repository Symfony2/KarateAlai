using System;
using System.Globalization;
using System.Windows.Data;

namespace KarateTournamentApp.Converters
{
    public class ExpanderIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                return isExpanded ? "Свернуть" : "Развернуть";
            }
            return "Свернуть";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
