namespace vatSysToolbox;

/// <summary>
/// Ignores all the errors. The toolbox services use it when no <see cref="IErrorReporter"/> is registered.
/// </summary>
public sealed class NullErrorReporter : IErrorReporter
{
    /// <summary>
    /// The one instance of the class.
    /// </summary>
    public static readonly NullErrorReporter Instance = new();

    NullErrorReporter()
    {
    }

    /// <inheritdoc/>
    public void ReportError(Exception exception)
    {
    }

    /// <inheritdoc/>
    public void ReportError(Exception exception, string message)
    {
    }
}
