using System.Drawing;
using System.Windows.Forms;
using vatsys;

namespace EurocatMenusPlugin.FlightPlan;

/// <summary>
/// Colours for the Eurocat look. The palette comes from the vatSys profile (which already
/// carries the real system's colours); what differs from stock vatSys controls is how the
/// colours are applied - see the notes on each member.
/// </summary>
static class EcTheme
{
    public static Color Background => Colours.GetColour(Colours.Identities.WindowBackground);

    /// <summary>Dark blue used for all labels, field text, enabled button text and the dark bevel edge.</summary>
    public static Color Text => Colours.GetColour(Colours.Identities.InteractiveText);

    /// <summary>Dark red used for the window title, the window outline and the outline of display-only fields.</summary>
    public static Color Title => Colours.GetColour(Colours.Identities.GenericText);

    /// <summary>Face colour of a selected (latched) button.</summary>
    public static Color Pressed => Colours.GetColour(Colours.Identities.WindowButtonDepressed);

    /// <summary>Outline drawn around the field that has keyboard focus.</summary>
    public static Color Focus => Colours.GetColour(Colours.Identities.HighlightedText);

    public static Color Warning => Colours.GetColour(Colours.Identities.WindowWarning);

    /// <summary>Light bevel edge (top-left of raised buttons, bottom-right of sunken fields).</summary>
    public static Color Light => Blend(Background, Color.White, 0.65);

    /// <summary>Dark bevel edge.</summary>
    public static Color Shadow => Text;

    /// <summary>Text of an unavailable button: slightly lighter than the face, not hatched.</summary>
    public static Color Unavailable => Blend(Background, Color.White, 0.30);

    /// <summary>Thin outline of a field that cannot be typed in.</summary>
    public static Color FlatBorder => Blend(Title, Background, 0.50);

    /// <summary>Dark half of an etched separator line.</summary>
    public static Color SeparatorDark => Blend(Text, Background, 0.45);

    public static Color Blend(Color from, Color to, double amount)
    {
        return Color.FromArgb(
            (int)Math.Round(from.R + (to.R - from.R) * amount),
            (int)Math.Round(from.G + (to.G - from.G) * amount),
            (int)Math.Round(from.B + (to.B - from.B) * amount));
    }
}

static class EcPaint
{
    /// <summary>Two-tone bevel just inside <paramref name="r"/>. Raised = light top-left, dark bottom-right.</summary>
    public static void Bevel(Graphics g, Rectangle r, bool raised, int thickness = 2)
    {
        Color topLeft = raised ? EcTheme.Light : EcTheme.Shadow;
        Color bottomRight = raised ? EcTheme.Shadow : EcTheme.Light;
        using (var tl = new SolidBrush(topLeft))
        using (var br = new SolidBrush(bottomRight))
        {
            for (int i = 0; i < thickness; i++)
            {
                // bottom and right first so the top-left colour wins the shared corners like Motif does
                g.FillRectangle(br, r.Left + i, r.Bottom - 1 - i, r.Width - 2 * i, 1);
                g.FillRectangle(br, r.Right - 1 - i, r.Top + i, 1, r.Height - 2 * i);
                g.FillRectangle(tl, r.Left + i, r.Top + i, r.Width - 2 * i - 1, 1);
                g.FillRectangle(tl, r.Left + i, r.Top + i, 1, r.Height - 2 * i - 1);
            }
        }
    }

    public static void Outline(Graphics g, Rectangle r, Color colour, int thickness = 1)
    {
        using (var brush = new SolidBrush(colour))
        {
            g.FillRectangle(brush, r.Left, r.Top, r.Width, thickness);
            g.FillRectangle(brush, r.Left, r.Bottom - thickness, r.Width, thickness);
            g.FillRectangle(brush, r.Left, r.Top, thickness, r.Height);
            g.FillRectangle(brush, r.Right - thickness, r.Top, thickness, r.Height);
        }
    }

    public static void CentredText(Graphics g, string text, Font font, Color colour, Rectangle r)
    {
        using (var brush = new SolidBrush(colour))
        using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            g.DrawString(text, font, brush, r, format);
        }
    }

    /// <summary>
    /// A boxed caption such as MOVE / CLOSE / LOWER. These sit flush in the header and footer:
    /// a plain dark-blue outline with no bevel, so they do not stand proud like the function buttons.
    /// </summary>
    public static void FlatBox(Graphics g, Rectangle r, string text, Font font, bool pressed)
    {
        using (var face = new SolidBrush(pressed ? EcTheme.Pressed : EcTheme.Background))
        {
            g.FillRectangle(face, r);
        }
        Outline(g, r, EcTheme.Text, 2);
        CentredText(g, text, font, EcTheme.Text, r);
    }
}

/// <summary>Label: always dark blue, whatever the state of the field beside it.</summary>
sealed class EcLabel : Label
{
    public EcLabel(string text, Font font)
    {
        Text = text;
        Font = font;
        AutoSize = false;
        ForeColor = EcTheme.Text;
        BackColor = EcTheme.Background;
        TextAlign = ContentAlignment.MiddleCenter;
        Margin = Padding.Empty;
        UseMnemonic = false;
    }
}

/// <summary>
/// Push button with the Eurocat bevel (light top-left, dark blue bottom-right). Can latch
/// (<see cref="Toggle"/>), in which case the face darkens and the bevel inverts.
/// </summary>
sealed class EcButton : Button
{
    bool _isChecked;
    bool _mouseDown;

    public EcButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        // Clicking a function button must not pull keyboard focus out of the data field.
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        Margin = Padding.Empty;
        BackColor = EcTheme.Background;
    }

    public bool Toggle { get; set; }

    /// <summary>Draw flush (plain outline, no bevel), as the header/footer captions are.</summary>
    public bool Flat { get; set; }

    public bool Checked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
            {
                return;
            }
            _isChecked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CheckedChanged;

    protected override void OnClick(EventArgs e)
    {
        if (Toggle)
        {
            Checked = !Checked;
        }
        base.OnClick(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        if (mevent.Button == MouseButtons.Left)
        {
            _mouseDown = true;
            // Refresh, not Invalidate: vatSys' UI thread is rarely idle, so a queued repaint
            // can lag visibly behind the click.
            Refresh();
        }
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _mouseDown = false;
        Refresh();
        base.OnMouseUp(mevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        _mouseDown = false;
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        Graphics g = pevent.Graphics;
        Rectangle r = ClientRectangle;
        bool sunken = _isChecked || (_mouseDown && Enabled);

        if (Flat)
        {
            EcPaint.FlatBox(g, r, Text, Font, sunken);
            return;
        }

        using (var face = new SolidBrush(sunken ? EcTheme.Pressed : EcTheme.Background))
        {
            g.FillRectangle(face, r);
        }
        EcPaint.Bevel(g, r, !sunken);

        // A latched button shows greyed text, as the selected CRE does on the real display.
        Color text = Enabled && !_isChecked ? EcTheme.Text : EcTheme.Unavailable;
        Rectangle textArea = Rectangle.Inflate(r, -2, -2);
        if (sunken)
        {
            textArea.Offset(1, 1);
        }
        EcPaint.CentredText(g, Text, Font, text, textArea);
    }
}

/// <summary>
/// Data field. Editable: sunken box (dark blue top-left, light bottom-right), with a
/// highlight ring when it has focus. Display-only: thin dark-red outline and no input.
/// Text is dark blue in both states.
/// </summary>
sealed class EcField : Control
{
    const int Ring = 2;
    const int BevelWidth = 2;
    const int PadX = 2;
    const int PadY = 1;

    readonly InnerBox _box;
    bool _editable;
    bool _error;

    public EcField(Font font, bool multiline = false)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.ContainerControl, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        Margin = Padding.Empty;
        BackColor = EcTheme.Background;

        _box = new InnerBox(this)
        {
            BorderStyle = BorderStyle.None,
            Font = font,
            CharacterCasing = CharacterCasing.Upper,
            Multiline = multiline,
            WordWrap = true,
            AcceptsReturn = false,
            ReadOnly = true,
            TabStop = false,
            BackColor = EcTheme.Background,
            ForeColor = EcTheme.Text,
            Margin = Padding.Empty
        };
        // Refresh so the focus ring follows the caret immediately (see EcButton.OnMouseDown).
        _box.GotFocus += (s, e) => Refresh();
        _box.LostFocus += (s, e) => Refresh();
        Controls.Add(_box);
    }

    static int Frame => Ring + BevelWidth;

    /// <summary>Whether the user can type in the field.</summary>
    public bool Editable
    {
        get => _editable;
        set
        {
            if (_editable == value)
            {
                return;
            }
            _editable = value;
            _box.ReadOnly = !value;
            _box.TabStop = value;
            if (!value && _box.Focused)
            {
                Form form = FindForm();
                if (form != null)
                {
                    form.ActiveControl = null;
                }
            }
            Invalidate();
        }
    }

    public bool NumericOnly { get; set; }

    public bool OctalOnly { get; set; }

    public int MaxLength
    {
        get => _box.MaxLength;
        set => _box.MaxLength = value;
    }

    /// <summary>True once the user has changed the text since it was last set from code.</summary>
    public bool Modified
    {
        get => _box.Modified;
        set => _box.Modified = value;
    }

    public override string Text
    {
        get => _box == null ? "" : _box.Text;
        set
        {
            if (_box != null)
            {
                _box.Text = value ?? "";
            }
        }
    }

    public IntPtr BoxHandle => _box.IsHandleCreated ? _box.Handle : IntPtr.Zero;

    public new bool Focus()
    {
        return _box.Focus();
    }

    public void SelectAll()
    {
        _box.SelectAll();
    }

    public void ErrorMode(bool on)
    {
        _error = on;
        _box.BackColor = on ? EcTheme.Warning : EcTheme.Background;
        Invalidate();
    }

    /// <summary>Sizes a single-line field to hold <paramref name="chars"/> characters.</summary>
    public void SizeForChars(int chars)
    {
        int two = TextRenderer.MeasureText("WW", _box.Font, Size.Empty, TextFormatFlags.NoPadding).Width;
        int one = TextRenderer.MeasureText("W", _box.Font, Size.Empty, TextFormatFlags.NoPadding).Width;
        int charWidth = Math.Max(1, two - one);
        Size = new Size(chars * charWidth + 2 * (Frame + PadX) + 3, _box.PreferredHeight + 2 * (Frame + PadY));
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        if (_editable)
        {
            _box.Focus();
        }
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (_box != null)
        {
            _box.Bounds = new Rectangle(Frame + PadX, Frame + PadY,
                Math.Max(1, Width - 2 * (Frame + PadX)), Math.Max(1, Height - 2 * (Frame + PadY)));
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(EcTheme.Background);

        Rectangle inner = Rectangle.Inflate(ClientRectangle, -Ring, -Ring);
        using (var fill = new SolidBrush(_error ? EcTheme.Warning : EcTheme.Background))
        {
            g.FillRectangle(fill, inner);
        }

        if (_editable)
        {
            EcPaint.Bevel(g, inner, false, BevelWidth);
            if (_box.Focused)
            {
                EcPaint.Outline(g, ClientRectangle, EcTheme.Focus, Ring);
            }
        }
        else
        {
            EcPaint.Outline(g, inner, EcTheme.FlatBorder);
        }
    }

    sealed class InnerBox : TextBox
    {
        const int WM_SETCURSOR = 0x0020;
        const int WM_LBUTTONDOWN = 0x0201;
        const int WM_LBUTTONDBLCLK = 0x0203;
        const int WM_RBUTTONDOWN = 0x0204;
        const int WM_RBUTTONDBLCLK = 0x0206;
        const int WM_MBUTTONDOWN = 0x0207;
        const int WM_CONTEXTMENU = 0x007B;

        readonly EcField owner;

        public InnerBox(EcField owner)
        {
            this.owner = owner;
        }

        protected override void WndProc(ref Message m)
        {
            if (!owner._editable)
            {
                switch (m.Msg)
                {
                    // A display-only field ignores the mouse, so it never takes focus or shows a caret.
                    case WM_LBUTTONDOWN:
                    case WM_LBUTTONDBLCLK:
                    case WM_RBUTTONDOWN:
                    case WM_RBUTTONDBLCLK:
                    case WM_MBUTTONDOWN:
                    case WM_CONTEXTMENU:
                        return;
                    case WM_SETCURSOR:
                        Cursor.Current = Cursors.Default;
                        m.Result = (IntPtr)1;
                        return;
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar))
            {
                if (owner.OctalOnly && (e.KeyChar < '0' || e.KeyChar > '7'))
                {
                    e.Handled = true;
                }
                else if (owner.NumericOnly && (e.KeyChar < '0' || e.KeyChar > '9'))
                {
                    e.Handled = true;
                }
            }
            base.OnKeyPress(e);
        }
    }
}

/// <summary>Narrow scroll strip beside a multi-line field: dark arrow blocks top and bottom with a trough between.</summary>
sealed class EcScrollBar : Control
{
    const int EM_LINESCROLL = 0x00B6;

    readonly EcField target;

    public EcScrollBar(EcField target)
    {
        this.target = target;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        BackColor = EcTheme.Background;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        IntPtr handle = target.BoxHandle;
        if (e.Button == MouseButtons.Left && handle != IntPtr.Zero)
        {
            SendMessage(handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)(e.Y < Height / 2 ? -1 : 1));
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(EcTheme.Background);

        int w = Width;
        var up = new Rectangle(0, 0, w, w);
        var down = new Rectangle(0, Height - w, w, w);
        var trough = new Rectangle(0, up.Bottom, w, Math.Max(0, down.Top - up.Bottom));

        if (trough.Height > 0)
        {
            EcPaint.Bevel(g, trough, false, 1);
        }

        using (var block = new SolidBrush(EcTheme.Shadow))
        using (var arrow = new SolidBrush(EcTheme.Light))
        {
            g.FillRectangle(block, up);
            g.FillRectangle(block, down);

            int m = Math.Max(2, w / 5);
            g.FillPolygon(arrow, new[]
            {
                new Point(up.Left + w / 2, up.Top + m),
                new Point(up.Right - m, up.Bottom - m),
                new Point(up.Left + m - 1, up.Bottom - m)
            });
            g.FillPolygon(arrow, new[]
            {
                new Point(down.Left + m - 1, down.Top + m),
                new Point(down.Right - m, down.Top + m),
                new Point(down.Left + w / 2, down.Bottom - m)
            });
        }
    }
}

/// <summary>Etched horizontal rule: dark line above a light line.</summary>
sealed class EcSeparator : Control
{
    public EcSeparator()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Selectable, false);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        TabStop = false;
        Height = 2;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using (var dark = new SolidBrush(EcTheme.SeparatorDark))
        using (var light = new SolidBrush(EcTheme.Light))
        {
            e.Graphics.FillRectangle(dark, 0, 0, Width, 1);
            e.Graphics.FillRectangle(light, 0, 1, Width, 1);
        }
    }
}
