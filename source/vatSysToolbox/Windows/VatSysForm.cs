using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using vatsys;

namespace vatSysToolbox.Windows;

/// <summary>
/// A vatSys form that hosts WPF content.
/// </summary>
public class VatSysForm : BaseForm
{
    /// <summary>
    /// Occurs when the user closes the form. Set <c>Cancel</c> to keep the form open.
    /// This event does not fire when you call <see cref="ForceClose"/>.
    /// </summary>
    public event FormClosingEventHandler? CustomFormClosing; // TODO: Rename

    // BaseForm draws the grip 11px tall, 12px above the bottom of the client area.
    const int SizeGripHeight = 12;

    readonly FrameworkElement _view;
    readonly IErrorReporter _errorReporter;

    bool ForceClosing { get; set; }

    readonly WindowHandle _windowHandle;

    /// <summary>
    /// Lets the view control this form.
    /// </summary>
    public IWindowHandle WindowHandle => _windowHandle;

    /// <param name="title">The text in the title bar.</param>
    /// <param name="createView">Makes the content of the form. It gets the <see cref="WindowHandle"/>.</param>
    /// <param name="errorReporter">
    /// Reports the exceptions that occur in the <see cref="IWindowHandle.BeforeClose"/> handlers, and when the form
    /// disposes the view model.
    /// </param>
    /// <param name="sizeToContent">
    /// If <c>true</c>, the form gets its size from the view, and the user cannot change the size.
    /// </param>
    /// <param name="canClose">If <c>false</c>, the form has no close button.</param>
    /// <param name="canMinimise">If <c>false</c>, the form has no minimise button.</param>
    /// <param name="middleClickToClose">If <c>true</c>, a middle-click on the form closes it.</param>
    public VatSysForm(
        string title,
        Func<IWindowHandle, FrameworkElement> createView,
        IErrorReporter errorReporter,
        bool sizeToContent,
        bool canClose = true,
        bool canMinimise = true,
        bool middleClickToClose = false)
    {
        Text = title;
        HasCloseButton = canClose;
        MinimizeBox = canMinimise;
        HasMinimizeButton = canMinimise;
        MiddleClickClose = middleClickToClose;

        _errorReporter = errorReporter;
        _windowHandle = new WindowHandle(this);
        _view = createView(_windowHandle);

        var elementHost = new ElementHost
        {
            Dock = DockStyle.Fill
        };

        if (sizeToContent)
        {
            // A Canvas measures its child with no size limit. The view then always has its natural size,
            // and SizeChanged occurs each time the content changes size.
            var canvas = new Canvas();
            canvas.Children.Add(_view);
            elementHost.Child = canvas;

            _view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            ResizeToContent(_view.DesiredSize);
            _view.SizeChanged += (_, e) => ResizeToContent(e.NewSize);

            FormBorderStyle = FormBorderStyle.FixedSingle;
            Padding = new Padding(0);
        }
        else
        {
            elementHost.Child = _view;

            FormBorderStyle = FormBorderStyle.Sizable;
            Resizeable = true;

            // When Resizeable is true, BaseForm.OnPaint draws a size grip in the bottom-right corner of the client
            // area. Leave a strip at the bottom so the ElementHost does not cover the grip.
            Padding = new Padding(0, 0, 0, SizeGripHeight);
        }

        Controls.Add(elementHost);

        Margin = new Padding(0);
    }

    /// <summary>
    /// Closes the form. <see cref="CustomFormClosing"/> does not fire, and the form is not hidden.
    /// </summary>
    public void ForceClose()
    {
        ForceClosing = true;
        HideOnClose = false;
        Close();
    }

    void ResizeToContent(Size contentSize)
    {
        if (WindowState != FormWindowState.Normal)
            return;

        // vatSys draws its own border and title bar. The ClientSize setter uses the standard Windows border to
        // calculate the form size, so it can give the wrong size. Use the real size of the border and title bar.
        var borderWidth = Width - ClientSize.Width;
        var borderHeight = Height - ClientSize.Height;

        Size = new System.Drawing.Size(
            (int)Math.Ceiling(contentSize.Width) + borderWidth,
            (int)Math.Ceiling(contentSize.Height) + borderHeight);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!ForceClosing && CustomFormClosing != null)
        {
            CustomFormClosing.Invoke(this, e);
            if (e.Cancel)
                return;
        }

        base.OnFormClosing(e);

        if (ForceClosing)
        {
            e.Cancel = false;
        }

        // The form is only hidden
        if (e.Cancel)
            return;

        _windowHandle.RaiseBeforeClose(e, canCancel: !ForceClosing, _errorReporter);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        ViewModelDisposer.Dispose(_view, _errorReporter);
    }
}
