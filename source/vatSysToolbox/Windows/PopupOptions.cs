namespace vatSysToolbox.Windows;

/// <summary>
/// The options of a popup that <see cref="WindowManager.ShowPopup"/> opens.
/// </summary>
public record PopupOptions
{
    /// <summary>
    /// The distance in screen pixels from the mouse cursor to the top-left corner of the popup.
    /// </summary>
    public System.Drawing.Size Offset { get; init; }

    /// <summary>
    /// If <c>true</c>, a middle-click on the popup closes it. A middle-click that the view handles does not close the
    /// popup.
    /// </summary>
    public bool MiddleClickToClose { get; init; } = true;
}
