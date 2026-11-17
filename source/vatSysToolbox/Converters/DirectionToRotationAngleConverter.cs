using System.Globalization;
using System.Windows.Data;
using vatSysToolbox.Controls;

namespace vatSysToolbox.Converters;

/// <summary>
/// Converts a <see cref="Direction"/> to a rotation angle in degrees, clockwise from <see cref="Direction.Up"/>.
/// </summary>
[ValueConversion(typeof(Direction), typeof(double))]
public class DirectionToRotationAngleConverter : IValueConverter
{
    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Direction direction)
        {
            return direction switch
            {
                Direction.Left => 270,
                Direction.Up => 0,
                Direction.Right => 90,
                Direction.Down => 180,
                _ => throw new ArgumentOutOfRangeException($"Unknown direction: {direction}")
            };
        }

        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
