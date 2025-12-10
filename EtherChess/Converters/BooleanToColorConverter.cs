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
            return isWhite ? Brushes.White : Brushes.Black;
        }
        return Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
