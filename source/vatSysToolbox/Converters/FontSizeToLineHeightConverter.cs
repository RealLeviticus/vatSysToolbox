using System.Windows.Data;

namespace vatSysToolbox.Converters;

/// <summary>
/// Converts a font size to a line height (0.875 times the font size).
/// </summary>
public class FontSizeToLineHeightConverter : IValueConverter
{
    const double ScaleFactor = 0.875;

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is null)
            throw new NotSupportedException();
        
        return double.Parse(value.ToString()) * ScaleFactor;
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}