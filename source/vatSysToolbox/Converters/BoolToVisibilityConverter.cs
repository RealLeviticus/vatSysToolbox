using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace vatSysToolbox.Converters;

/// <summary>
/// Converts <c>true</c> to <see cref="Visibility.Visible"/>, and <c>false</c> to <see cref="Visibility.Collapsed"/>.
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool boolValue)
            throw new NotSupportedException();
        
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}