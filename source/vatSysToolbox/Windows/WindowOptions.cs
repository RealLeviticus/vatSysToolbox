namespace vatSysToolbox.Windows;

/// <summary>
/// The options of a window that <see cref="WindowManager"/> opens.
/// </summary>
public record WindowOptions
{
    /// <summary>
    /// How the window gets its size. The default is <see cref="WindowSize.Content"/>.
    /// </summary>
    public WindowSize Size { get; init; } = WindowSize.Content;

    public bool CanClose { get; init; } = true;

    public bool CanMinimise { get; init; } = true;

    /// <summary>
    /// If <c>true</c>, a middle-click on window closes it.
    /// </summary>
    public bool MiddleClickToClose { get; init; }
}
