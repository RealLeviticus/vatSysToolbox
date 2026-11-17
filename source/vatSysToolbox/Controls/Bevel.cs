using System.Windows;
using System.Windows.Media;

namespace vatSysToolbox.Controls;

/// <summary>
/// The brushes and the sizes for the beveled (3D) edges of the vatSys controls.
/// </summary>
public static class Bevel
{
    /// <summary>
    /// The opacity of <see cref="LightBrush"/> and <see cref="DarkBrush"/>.
    /// </summary>
    public static float Alpha = 0.4f;

    /// <summary>
    /// The brush for the edges that face the light (the top and left edges of a raised control).
    /// </summary>
    public static Brush LightBrush = new SolidColorBrush(Color.FromScRgb(Alpha, 255, 255, 255));

    /// <summary>
    /// The brush for the edges that face away from the light (the bottom and right edges of a raised control).
    /// </summary>
    public static Brush DarkBrush = new SolidColorBrush(Color.FromScRgb(Alpha, 0, 0, 0));

    /// <summary>
    /// The width of the bevel on the edges of a control.
    /// </summary>
    public static Thickness BorderThickness = new(2);

    /// <summary>
    /// The width of a beveled line.
    /// </summary>
    public static double LineWidth = 4;
}
