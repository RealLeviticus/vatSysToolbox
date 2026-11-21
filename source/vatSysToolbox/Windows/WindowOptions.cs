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

    /// <summary>
    /// If <c>false</c>, the window has no close button.
    /// </summary>
    public bool CanClose { get; init; } = true;

    /// <summary>
    /// If <c>false</c>, the window has no minimise button.
    /// </summary>
    public bool CanMinimise { get; init; } = true;

    /// <summary>
    /// If <c>false</c>, the window has no send-to-back button.
    /// </summary>
    public bool CanSendToBack { get; init; } = true;

    /// <summary>
    /// If <c>true</c>, a middle-click on window closes it.
    /// </summary>
    public bool MiddleClickToClose { get; init; }

    /// <summary>
    /// The smallest size that the user can give the window. If <c>null</c>, the window has no minimum size.
    /// Use it only with <see cref="WindowSize.Absolute"/>.
    /// </summary>
    public System.Windows.Size? MinimumSize { get; init; }

    /// <summary>
    /// The largest size that the user can give the window. If <c>null</c>, the window has no maximum size.
    /// Use it only with <see cref="WindowSize.Absolute"/>.
    /// </summary>
    public System.Windows.Size? MaximumSize { get; init; }
}
