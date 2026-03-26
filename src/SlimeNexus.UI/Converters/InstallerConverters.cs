using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SlimeNexus.UI.Converters;

/// <summary>
/// Converter that returns different values based on a boolean input.
/// Parameter format: "TrueValue|FalseValue"
/// </summary>
public class BoolToStringConverter : IValueConverter
{
    public static BoolToStringConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string paramString)
        {
            var parts = paramString.Split('|');
            if (parts.Length == 2)
            {
                return boolValue ? parts[0] : parts[1];
            }
        }
        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Multiplies a double value by the parameter.
/// </summary>
public class MultiplyConverter : IValueConverter
{
    public static MultiplyConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double doubleValue && parameter is string paramString && double.TryParse(paramString, out var multiplier))
        {
            return doubleValue * multiplier;
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a color name string to a SolidColorBrush.
/// Supports: Green, Red, Orange, Gray, White, and hex colors.
/// </summary>
public class StringToColorBrushConverter : IValueConverter
{
    public static StringToColorBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string colorName)
            return new SolidColorBrush(Colors.White);

        return colorName.ToLowerInvariant() switch
        {
            "green" => new SolidColorBrush(Color.Parse("#00b894")),
            "red" => new SolidColorBrush(Color.Parse("#e74c3c")),
            "orange" => new SolidColorBrush(Color.Parse("#f39c12")),
            "gray" or "grey" => new SolidColorBrush(Color.Parse("#808080")),
            "white" => new SolidColorBrush(Colors.White),
            "purple" => new SolidColorBrush(Color.Parse("#6c5ce7")),
            "blue" => new SolidColorBrush(Color.Parse("#3498db")),
            _ when colorName.StartsWith('#') => new SolidColorBrush(Color.Parse(colorName)),
            _ => new SolidColorBrush(Colors.White)
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
