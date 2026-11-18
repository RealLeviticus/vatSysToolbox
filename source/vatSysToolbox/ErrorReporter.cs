using Microsoft.Extensions.Logging;
using vatSysToolbox.Infrastructure;

namespace vatSysToolbox;

/// <summary>
/// Reports errors to the user and to the log.
/// </summary>
public interface IErrorReporter
{
    /// <summary>
    /// Reports the exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    void ReportError(Exception exception);

    /// <inheritdoc cref="ReportError(Exception)"/>
    /// <param name="message">The message for the log.</param>
    void ReportError(Exception exception, string message);
}

/// <summary>
/// Writes errors to the log, and shows them in the vatSys error window.
/// The error window shows each error message one time each minute at most, so that the same error does not flood
/// the error window. You can use this class from all threads.
/// </summary>
/// <param name="source">The name that shows with the errors in the vatSys error window.</param>
/// <param name="logger">Writes the errors to the log.</param>
/// <param name="clock">Gives the time, to find when an error showed last.</param>
public class ErrorReporter(string source, ILogger<ErrorReporter> logger, IClock clock) : IErrorReporter
{
    static readonly TimeSpan RepeatInterval = TimeSpan.FromMinutes(1);

    readonly Dictionary<string, DateTimeOffset> _lastShownTimes = new();

    /// <inheritdoc/>
    public void ReportError(Exception exception)
    {
        logger.LogError(exception, "An error occurred");
        TryShowError(exception);
    }

    /// <inheritdoc/>
    public void ReportError(Exception exception, string message)
    {
        logger.LogError(exception, message);
        TryShowError(exception);
    }

    void TryShowError(Exception exception)
    {
        lock (_lastShownTimes)
        {
            var now = clock.UtcNow();

            if (_lastShownTimes.TryGetValue(exception.Message, out var lastShown) &&
                now - lastShown <= RepeatInterval)
                return;

            vatsys.Errors.Add(exception, source);
            _lastShownTimes[exception.Message] = now;
        }
    }
}
