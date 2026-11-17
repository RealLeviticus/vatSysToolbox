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
    event EventHandler? Moved;

    /// <summary>
    /// Occurs on the UI thread when the window is about to close. Set <see cref="CancelEventArgs.Cancel"/> to
    /// <c>true</c> to keep the window open. It does not occur when the window is only hidden.
    /// </summary>
    /// <remarks>
    /// Some closes cannot be canceled, and the window closes when a handler sets <c>Cancel</c>. For example, when
    /// the window manager closes the window, or when vatSys closes.
    /// </remarks>
    event EventHandler<CancelEventArgs>? BeforeClose;

    void Focus();
    void Close();
}

public class WindowHandle : IWindowHandle
{
    readonly VatSysForm _form;

    public WindowHandle(VatSysForm form)
    {
        _form = form;
        _form.Shown += (_, _) => Shown?.Invoke(this, EventArgs.Empty);
        _form.Move += (_, _) => Moved?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Shown;
    public event EventHandler? Moved;

    public event EventHandler<CancelEventArgs>? BeforeClose;

    public void Focus()
    {
        _form.Show();
        _form.WindowState = FormWindowState.Normal;
        _form.Activate();
    }

    public void Close()
    {
        _form.ForceClose();
    }

    internal void RaiseBeforeClose(FormClosingEventArgs e, bool canCancel, IErrorReporter errorReporter)
    {
        BeforeCloseEvent.Raise(BeforeClose, this, e, canCancel, errorReporter);
    }
}