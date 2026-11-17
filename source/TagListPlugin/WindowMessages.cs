namespace TagListPlugin;

// TagListViewModel sends these messages with WeakReferenceMessenger. Plugin receives them, and shows the state of the
// Tag List to the user and to the other plugins.

/// <summary>
/// The Tag List window shows.
/// </summary>
public record TagListOpenedMessage;

/// <summary>
/// The Tag List window is about to close.
/// </summary>
public record TagListClosedMessage;
