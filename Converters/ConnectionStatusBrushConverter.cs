using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EtherChess.Converters;

public class ConnectionStatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var connected = value is bool b && b;
        var color = connected
            ? Color.FromRgb(0x81, 0xB6, 0x4C)
            : Color.FromRgb(0xF6, 0xF6, 0x69);
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
