using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Input;

namespace vatSysToolbox.Windows;

/// <summary>
/// A form with no border or title bar, at the mouse cursor (plus an offset). The form closes when it is not the active
/// form (for example, when the user clicks outside it), and when the user middle-clicks it. When the form closes, it
/// disposes the view model of the content (<see cref="FrameworkElement.DataContext"/>) if the view model is
/// <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>.
/// </summary>
public class PopupForm : Form
{
    readonly FrameworkElement _view;
    readonly IErrorReporter _errorReporter;
    readonly PopupWindowHandle _windowHandle;

    bool _isClosing;

    public IWindowHandle WindowHandle => _windowHandle;

    public PopupForm(Func<IWindowHandle, FrameworkElement> createView, IErrorReporter errorReporter, PopupOptions options)
    {
        _errorReporter = errorReporter;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Location = Cursor.Position + options.Offset;

        _windowHandle = new PopupWindowHandle(this);
        _view = createView(_windowHandle);

        // A Canvas measures its child with no size limit. The view then always has its natural size, and SizeChanged
        // occurs when WPF has done the layout of the view.
        var canvas = new Canvas();
        canvas.Children.Add(_view);

        // The popup has no title bar. Thus, a middle-click on the content closes it. MouseDown bubbles up from the view,
        // so a middle-click that the view handles does not get to the canvas.
        if (options.MiddleClickToClose)
        {
            canvas.MouseDown += (_, e) =>
            {
                if (e.ChangedButton != MouseButton.Middle)
                    return;

                e.Handled = true;
                WindowHandle.Close();
            };
        }

        var elementHost = new ElementHost
        {
            Dock = DockStyle.Fill,
            Child = canvas
        };
        Controls.Add(elementHost);

        _view.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        ResizeToContent(_view.DesiredSize);
        _view.SizeChanged += (_, e) => ResizeToContent(e.NewSize);
    }

    void ResizeToContent(System.Windows.Size contentSize)
    {
        ClientSize = new System.Drawing.Size(
            (int)Math.Ceiling(contentSize.Width),
            (int)Math.Ceiling(contentSize.Height));
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);

        if (!e.Cancel)
            _windowHandle.RaiseBeforeClose(e, canCancel: true, _errorReporter);

        _isClosing = !e.Cancel;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        ViewModelDisposer.Dispose(_view, _errorReporter);
    }

    // The form also deactivates while it closes. Do not close it a second time.
    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);

        if (!_isClosing && !IsDisposed)
            Close();
    }

    class PopupWindowHandle : IWindowHandle
    {
        readonly PopupForm _form;

        public PopupWindowHandle(PopupForm form)
        {
            _form = form;
            _form.Shown += (_, _) => Shown?.Invoke(this, EventArgs.Empty);
            _form.Move += (_, _) => Moved?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? Shown;
        public event EventHandler? Moved;

        public event EventHandler<CancelEventArgs>? BeforeClose;

        public void Focus() => _form.Activate();

        // The popup can close before the view model calls this method (for example, when the user clicks outside it)
        public void Close()
        {
            if (!_form._isClosing && !_form.IsDisposed)
                _form.Close();
        }

        internal void RaiseBeforeClose(FormClosingEventArgs e, bool canCancel, IErrorReporter errorReporter)
        {
            BeforeCloseEvent.Raise(BeforeClose, this, e, canCancel, errorReporter);
        }
    }
}
