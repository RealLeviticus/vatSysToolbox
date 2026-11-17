using System.Windows;

namespace vatSysToolbox.Windows;

/// <summary>
/// How a window that <see cref="WindowManager"/> opens gets its size.
/// </summary>
public abstract record WindowSize
{
    // Only the types in this record can derive from it
    WindowSize() { }

    /// <summary>
    /// The window gets its size from the view. The user cannot change the size.
    /// </summary>
    public static WindowSize Content { get; } = new ContentSize();

    /// <summary>
    /// The window opens with this size the first time, and with its last size after that. The user can change the size.
    /// </summary>
    public static WindowSize Absolute(double width, double height) => new AbsoluteSize(new Size(width, height));

    public sealed record ContentSize : WindowSize;

    public sealed record AbsoluteSize(Size Size) : WindowSize;
}
