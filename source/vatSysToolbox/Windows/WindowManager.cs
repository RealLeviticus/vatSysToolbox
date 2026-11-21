using System.Windows;
using System.Windows.Forms;
using vatsys;

namespace vatSysToolbox.Windows;

/// <summary>
/// Opens and closes <see cref="VatSysForm"/> windows. A key identifies each window, so that only one window with
/// the same key is open at a time. The window manager keeps the last location and size of each window, and opens the
/// window at that location again. A window that sizes to its content gets its size from the content, and not the last
/// size.
/// </summary>
/// <param name="guiInvoker">Opens and closes the windows on the UI thread.</param>
/// <param name="errorReporter">Reports the exceptions that occur in the windows.</param>
public class WindowManager(IGuiInvoker guiInvoker, IErrorReporter errorReporter)
{
    readonly Dictionary<string, VatSysForm> _forms = new();
    readonly Dictionary<string, System.Drawing.Rectangle> _lastBounds = new();
    readonly object _gate = new();

    /// <summary>
    /// Opens a window. If a window with the same key is already open, this method does nothing.
    /// </summary>
    /// <param name="key">Identifies the window.</param>
    /// <param name="title">The text in the title bar.</param>
    /// <param name="createView">Makes the content of the window. It gets the handle of the window.</param>
    /// <param name="options">The options of the window. If <c>null</c>, the window uses the default options.</param>
    public void Open(
        string key,
        string title,
        Func<IWindowHandle, FrameworkElement> createView,
        WindowOptions? options = null)
    {
        // Save a call to the UI thread if the window is already open
        if (IsOpen(key))
            return;

        guiInvoker.InvokeOnGUI(mainForm =>
        {
            lock (_gate)
            {
                if (_forms.ContainsKey(key))
                    return;

                CreateWindow(mainForm, key, title, createView, options ?? new WindowOptions());
            }
        });
    }

    /// <summary>
    /// Opens a window. If a window with the same key is open, the existing window is focused instead.
    /// </summary>
    /// <param name="key">Identifies the window.</param>
    /// <param name="title">The text in the title bar.</param>
    /// <param name="createView">Makes the content of the window. It gets the handle of the window.</param>
    /// <param name="options">The options of the window. If <c>null</c>, the window uses the default options.</param>
    public void OpenOrFocus(
        string key,
        string title,
        Func<IWindowHandle, FrameworkElement> createView,
        WindowOptions? options = null)
    {
        guiInvoker.InvokeOnGUI(mainForm =>
        {
            lock (_gate)
            {
                if (_forms.TryGetValue(key, out var form))
                {
                    form.WindowHandle.Focus();
                    return;
                }

                CreateWindow(mainForm, key, title, createView, options ?? new WindowOptions());
            }
        });
    }

    /// <summary>
    /// Opens a window if it is not open, and closes it if it is open. If the window is minimized or hidden, this
    /// method focuses it.
    /// </summary>
    /// <param name="key">Identifies the window.</param>
    /// <param name="title">The text in the title bar.</param>
    /// <param name="createView">Makes the content of the window. It gets the handle of the window.</param>
    /// <param name="options">The options of the window. If <c>null</c>, the window uses the default options.</param>
    public void Toggle(
        string key,
        string title,
        Func<IWindowHandle, FrameworkElement> createView,
        WindowOptions? options = null)
    {
        guiInvoker.InvokeOnGUI(mainForm =>
        {
            lock (_gate)
            {
                if (!_forms.TryGetValue(key, out var form))
                {
                    CreateWindow(mainForm, key, title, createView, options ?? new WindowOptions());
                    return;
                }

                if (!form.Visible || form.WindowState == FormWindowState.Minimized)
                {
                    form.WindowHandle.Focus();
                    return;
                }

                form.ForceClose();
            }
        });
    }

    /// <summary>
    /// Opens a <see cref="PopupForm"/> at the mouse cursor.
    /// </summary>
    /// <param name="createView">Makes the content of the popup. It gets the handle of the popup.</param>
    /// <param name="options">The options of the popup. If <c>null</c>, the popup uses the default options.</param>
    public void ShowPopup(Func<IWindowHandle, FrameworkElement> createView, PopupOptions? options = null)
    {
        guiInvoker.InvokeOnGUI(mainForm =>
        {
            var form = new PopupForm(createView, errorReporter, options ?? new PopupOptions());
            MMI.EnsureWindowVisible(form);
            form.Show(mainForm);
        });
    }

    /// <summary>
    /// Returns <c>true</c> if a window with the key is open.
    /// </summary>
    /// <param name="key">Identifies the window.</param>
    public bool IsOpen(string key)
    {
        lock (_gate)
        {
            return _forms.ContainsKey(key);
        }
    }

    /// <summary>
    /// Closes the window with the key. If no window with the key is open, this method does nothing.
    /// </summary>
    /// <param name="key">Identifies the window.</param>
    public void Close(string key)
    {
        VatSysForm? form;
        lock (_gate)
        {
            // Do not post to the UI thread if the window is not open
            if (!_forms.TryGetValue(key, out form))
                return;
        }

        guiInvoker.InvokeOnGUI(_ =>
        {
            try
            {
                form.ForceClose();
            }
            catch (ObjectDisposedException)
            {
                // Form may have already been closed or disposed
            }
        });
    }

    void CreateWindow(
        Form mainForm,
        string key,
        string title,
        Func<IWindowHandle, FrameworkElement> createView,
        WindowOptions options)
    {
        var form = new VatSysForm(
            title,
            createView,
            errorReporter,
            sizeToContent: options.Size is WindowSize.ContentSize,
            options.CanClose,
            options.CanMinimise,
            options.MiddleClickToClose,
            options.CanSendToBack)
        {
            StartPosition = FormStartPosition.CenterParent
        };

        // If the view closes the form before it has a handle, the form is disposed and cannot be shown.
        if (form.IsDisposed)
            return;

        // Set the limits before the size, so that the form keeps the size in the limits
        if (options.MinimumSize is { } minimumSize)
            form.MinimumSize = ToDrawingSize(minimumSize);

        if (options.MaximumSize is { } maximumSize)
            form.MaximumSize = ToDrawingSize(maximumSize);

        if (options.Size is WindowSize.AbsoluteSize absoluteSize)
        {
            form.Width = (int)absoluteSize.Size.Width;
            form.Height = (int)absoluteSize.Size.Height;
        }

        if (_lastBounds.TryGetValue(key, out var lastBounds))
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = lastBounds.Location;

            // A window that sizes to its content gets its size from the content, and not the last size
            if (options.Size is WindowSize.AbsoluteSize)
                form.Size = lastBounds.Size;
        }

        form.PerformLayout();
        form.Closed += (_, _) => OnWindowClosed(key, form);

        _forms[key] = form;
        form.Show(mainForm);
    }

    static System.Drawing.Size ToDrawingSize(Size size) => new((int)size.Width, (int)size.Height);

    void OnWindowClosed(string key, VatSysForm form)
    {
        lock (_gate)
        {
            // If the window is minimised or maximised, Bounds is not the normal position and size of the window
            _lastBounds[key] = form.WindowState == FormWindowState.Normal
                ? form.Bounds
                : form.RestoreBounds;

            _forms.Remove(key);
        }
    }
}
