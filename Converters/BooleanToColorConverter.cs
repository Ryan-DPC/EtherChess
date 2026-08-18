using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EtherChess.Converters;

public class BooleanToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isWhite)
        {
            return isWhite ? new SolidColorBrush(Color.FromRgb(0xFF, 0xF8, 0xEE)) : new SolidColorBrush(Color.FromRgb(0x1C, 0x12, 0x0C));
        }
        return Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
