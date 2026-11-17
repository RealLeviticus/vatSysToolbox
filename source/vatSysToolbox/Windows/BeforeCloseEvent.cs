using System.ComponentModel;
using System.Windows.Forms;

namespace vatSysToolbox.Windows;

/// <summary>
/// Raises the <see cref="IWindowHandle.BeforeClose"/> event for the window handles.
/// </summary>
static class BeforeCloseEvent
{
    /// <summary>
    /// Runs each handler, and cancels the close if a handler sets <see cref="CancelEventArgs.Cancel"/> and the close
    /// can be cancelled.
    /// </summary>
    public static void Raise(
        EventHandler<CancelEventArgs>? beforeClose,
        object sender,
        FormClosingEventArgs e,
        bool canCancel,
        IErrorReporter errorReporter)
    {
        if (beforeClose is null)
            return;

        var args = new CancelEventArgs();

        // Run each handler on its own, so that an exception in one handler does not stop the others.
        // An exception that is not caught on the UI thread stops vatSys.
        foreach (EventHandler<CancelEventArgs> handler in beforeClose.GetInvocationList())
        {
            try
            {
                handler(sender, args);
            }
            catch (Exception ex)
            {
                errorReporter.ReportError(ex, "Could not run the code before the window closes");
            }
        }

        // Do not keep the window open when vatSys or the owner form closes
        var isUserClose = e.CloseReason is CloseReason.UserClosing or CloseReason.None;
        if (args.Cancel && canCancel && isUserClose)
            e.Cancel = true;
    }
}