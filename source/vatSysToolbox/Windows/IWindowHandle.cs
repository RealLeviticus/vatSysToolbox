using System.ComponentModel;
using System.Windows.Forms;

namespace vatSysToolbox.Windows;

/// <summary>
/// Lets a view or a view model control its window.
/// </summary>
public interface IWindowHandle
{
    /// <summary>
    /// Occurs when the window shows for the first time.
    /// </summary>
    event EventHandler? Shown;

    /// <summary>
    /// Occurs when the window moves.
    /// </summary>
    event EventHandler? Moved;

    /// <summary>
    /// Occurs when the window is about to close. Set <see cref="CancelEventArgs.Cancel"/> to <c>true</c> to keep the
    /// window open.
    /// </summary>
    /// <remarks>
    /// Some closes cannot be canceled, and the window closes when a handler sets <c>Cancel</c>. For example, when
    /// the window manager closes the window, or when vatSys closes.
    /// </remarks>
    event EventHandler<CancelEventArgs>? BeforeClose;

    /// <summary>
    /// Shows the window, restores it if it is minimized, and makes it the active window.
    /// </summary>
    void Focus();

    /// <summary>
    /// Closes the window. <see cref="BeforeClose"/> occurs, but it cannot keep the window open.
    /// </summary>
    void Close();
}

/// <summary>
/// The <see cref="IWindowHandle"/> of a <see cref="VatSysForm"/>.
/// </summary>
public class WindowHandle : IWindowHandle
{
    readonly VatSysForm _form;

    /// <summary>
    /// Makes the handle of the form.
    /// </summary>
    /// <param name="form">The form that the handle controls.</param>
    public WindowHandle(VatSysForm form)
    {
        _form = form;
        _form.Shown += (_, _) => Shown?.Invoke(this, EventArgs.Empty);
        _form.Move += (_, _) => Moved?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public event EventHandler? Shown;
    /// <inheritdoc/>
    public event EventHandler? Moved;

    /// <inheritdoc/>
    public event EventHandler<CancelEventArgs>? BeforeClose;

    /// <inheritdoc/>
    public void Focus()
    {
        _form.Show();
        _form.WindowState = FormWindowState.Normal;
        _form.Activate();
    }

    /// <inheritdoc/>
    public void Close()
    {
        _form.ForceClose();
    }

    internal void RaiseBeforeClose(FormClosingEventArgs e, bool canCancel, IErrorReporter errorReporter)
    {
        BeforeCloseEvent.Raise(BeforeClose, this, e, canCancel, errorReporter);
    }
}