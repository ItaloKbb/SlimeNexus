using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SlimeNexus.UI.Converters;

/// <summary>
/// Converts a boolean to a color (green for true, red for false).
/// </summary>
public class BoolToGreenRedConverter : IValueConverter
{
    public static readonly BoolToGreenRedConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Colors.LimeGreen : Colors.Red;
        }
        return Colors.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
