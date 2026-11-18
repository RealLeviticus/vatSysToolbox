namespace vatSysToolbox.Infrastructure;

/// <summary>
/// Gives the current time. Use a different clock in tests.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gives the current time in UTC.
    /// </summary>
    DateTimeOffset UtcNow();
}

/// <summary>
/// Gives the time of the system clock.
/// </summary>
public class SystemClock : IClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow() => DateTimeOffset.UtcNow;
}
