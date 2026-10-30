using System;
using System.Globalization;
using System.Windows.Data;
using KarateTournamentApp.Localization;

namespace KarateTournamentApp.Converters;

public sealed class EnumToRussianTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => RussianText.DisplayEnum(value);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
