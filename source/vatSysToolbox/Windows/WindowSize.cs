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
    /// <param name="width">The width when the window opens the first time.</param>
    /// <param name="height">The height when the window opens the first time.</param>
    public static WindowSize Absolute(double width, double height) => new AbsoluteSize(new Size(width, height));

    /// <summary>
    /// See <see cref="Content"/>.
    /// </summary>
    public sealed record ContentSize : WindowSize;

    /// <summary>
    /// See <see cref="Absolute"/>.
    /// </summary>
    /// <param name="Size">The size of the window when it opens the first time.</param>
    public sealed record AbsoluteSize(Size Size) : WindowSize;
}
