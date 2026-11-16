using System.Windows;

namespace vatSysToolbox.Windows;

/// <summary>
/// Disposes the view model of a view when its form closes.
/// </summary>
static class ViewModelDisposer
{
    /// <summary>
    /// Disposes the <see cref="FrameworkElement.DataContext"/> of the view if it is <see cref="IAsyncDisposable"/> or
    /// <see cref="IDisposable"/>. This method does not wait for an async disposal to complete.
    /// The error reporter receives the errors from the disposal.
    /// </summary>
    public static void Dispose(FrameworkElement view, IErrorReporter errorReporter)
    {
        switch (view.DataContext)
        {
            case IAsyncDisposable asyncDisposable:
                _ = DisposeAsync(asyncDisposable, errorReporter);
                break;

            case IDisposable disposable:
                // An exception that is not caught on the UI thread stops vatSys.
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    errorReporter.ReportError(ex, "Could not dispose the view model");
                }

                break;
        }
    }

    static async Task DisposeAsync(IAsyncDisposable asyncDisposable, IErrorReporter errorReporter)
    {
        try
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            errorReporter.ReportError(ex, "Could not dispose the view model");
        }
    }
}
