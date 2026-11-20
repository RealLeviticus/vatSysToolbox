using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using vatsys;
using vatSysToolbox;
using States = vatsys.FDP2.FDR.FDRStates;
using Timer = System.Windows.Forms.Timer;

namespace EurocatMenusPlugin.FlightPlan;

/// <summary>
/// A Flight Plan Window with the layout of the Eurocat window. It does the same vatSys actions as the vatSys Flight
/// Plan Window. <see cref="FlightPlanWindowPatches"/> shows it in place of the vatSys window.
/// </summary>
/// <remarks>
/// This is a WinForms window, and not a WPF view, so that the bevels, the outlines, and the title bar are drawn the
/// same as in the Eurocat window. The colours come from the vatSys profile.
/// </remarks>
public class FlightPlanWindow : BaseForm
{
    enum Mode
    {
        View,
        Create,
        Modify,
        Depart
    }

    // ---- layout constants -------------------------------------------------
    const int Pad = 8;
    const int BtnW = 46;
    const int BtnH = 32;
    const int BtnGap = 2;
    const int GroupGap = 12;
    const int TimeColW = 96;

    static readonly HashSet<string> WatchedProperties = new HashSet<string>
    {
        nameof(FDP2.FDR.State), nameof(FDP2.FDR.ControllerTracking), nameof(FDP2.FDR.ControllingSector),
        nameof(FDP2.FDR.AircraftCount), nameof(FDP2.FDR.AircraftType), nameof(FDP2.FDR.AircraftWake),
        nameof(FDP2.FDR.AircraftEquip), nameof(FDP2.FDR.AircraftSurvEquip), nameof(FDP2.FDR.DepAirport),
        nameof(FDP2.FDR.DesAirport), nameof(FDP2.FDR.AltAirport), nameof(FDP2.FDR.ETD), nameof(FDP2.FDR.ATD),
        nameof(FDP2.FDR.EET), nameof(FDP2.FDR.TAS), nameof(FDP2.FDR.RFL), nameof(FDP2.FDR.Route),
        nameof(FDP2.FDR.ParsedRoute), nameof(FDP2.FDR.Remarks), nameof(FDP2.FDR.FlightRules),
        nameof(FDP2.FDR.AssignedSSRCode), nameof(FDP2.FDR.LabelOpData), nameof(FDP2.FDR.CoupledTrack),
        nameof(FDP2.FDR.InhibitAutoCouple), nameof(FDP2.FDR.Callsign)
    };

    readonly Font _font = MMI.eurofont_winverysml;
    readonly IErrorReporter _errorReporter;

    // ---- state ------------------------------------------------------------
    FDP2.FDR? _fdr;
    Mode _mode = Mode.View;
    bool _updating;
    bool _requestPending;
    Timer? _requestTimer;
    Action? _unsubscribeNoFp;
    Track? _routeTrack;

    // ---- function buttons ---------------------------------------------------
    EcButton _nextButton, _prevButton;
    EcButton _creButton, _modButton, _depButton;
    EcButton _crteButton, _rplButton, _estButton, _holdButton, _steButton, _backButton, _prlButton, _tmaButton, _cflButton;
    EcButton _accButton, _cnlButton, _cbepButton, _hndButton, _inhiButton, _exitButton, _hndiButton, _supButton;
    EcButton _ncouButton, _bcouButton;
    EcButton _trsButton, _jtrtButton, _aurButton, _printStripButton, _cmcButton, _fmfButton, _printFdrButton;
    EcButton _rteButton, _clearButton, _tpcfButton, _enterButton, _escapeButton;
    EcButton _lowerButton;
    EcLabel _counterLabel, _stateLabel, _updateTimeLabel, _errorLabel;
    Panel _errorPanel;

    // ---- title bar (drawn in the non-client area) -----------------------------
    Rectangle _closeBox;
    bool _closePressed;

    // ---- fields -------------------------------------------------------------
    EcField _lplField, _csecField, _pssrField, _assrField, _opdataField, _atdField;
    EcField _acidField, _frulField, _typ1Field, _typ2Field, _typ3Field, _adepField, _etdField, _adesField, _rflField;
    EcField _rteField, _otherField;
    EcField _ftypField, _equip1Field, _equip2Field, _tasField, _eetField, _altn1Field, _altn2Field, _regnField;

    EcField[] _planFields;

    /// <summary>
    /// Opens a new window. Call this method on the UI thread.
    /// </summary>
    /// <param name="fdr">The flight plan to show, or <c>null</c> for an empty window.</param>
    /// <param name="errorReporter">Reports the exceptions that occur in the window.</param>
    public static void Open(FDP2.FDR? fdr, IErrorReporter errorReporter)
    {
        var window = new FlightPlanWindow(errorReporter);
        if (fdr != null)
        {
            window.LoadFDR(fdr);
        }

        Form? owner = FlightPlanInternals.MainForm;
        if (owner != null && !owner.IsDisposed)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }

        // Draw everything before returning rather than leaving it to the message queue.
        window.FlushPaint(true);
    }

    // BuildLayout makes the controls. The constructor calls it, thus the control fields are never null after the
    // constructor. The compiler does not see that.
#pragma warning disable CS8618
    /// <param name="errorReporter">Reports the exceptions that occur in the window.</param>
    public FlightPlanWindow(IErrorReporter errorReporter)
#pragma warning restore CS8618
    {
        _errorReporter = errorReporter;

        SuspendLayout();
        Name = "EurocatFlightPlanWindow";
        Text = "FLIGHT PLAN WINDOW";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        StartPosition = FormStartPosition.CenterScreen;
        Resizeable = false;
        BackColor = EcTheme.Background;
        MiddleClickClose = false;

        // The title bar is drawn Eurocat-style (MOVE / title / CLOSE) by the overrides below, which
        // replace vatSys' own title-bar buttons. HasCloseButton must stay true all the same:
        // BaseForm cancels every close request for a window that has it switched off.
        HasCloseButton = true;
        HasMinimizeButton = false;
        HasMaximizeButton = false;
        HasSendBackButton = false;
        TitleTextColor = EcTheme.Title;
        BorderColor = EcTheme.Title;

        BuildLayout();
        ResumeLayout(false);

        FDP2.FDRsChanged += FDP2_FDRsChanged;
        Network.ValidATCChanged += Network_ValidATCChanged;
        MMI.SelectedTrackChanged += MMI_SelectedTrackChanged;

        LoadFDR(null);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_acidField.Editable)
        {
            _acidField.Focus();
        }
        FlushPaint(true);
    }

    [DllImport("user32.dll")]
    static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

    /// <summary>
    /// Paints the window and every child control right now.
    ///
    /// Windows only delivers WM_PAINT when a thread's message queue is otherwise empty, and each
    /// child control needs its own. vatSys' UI thread is rarely idle, so left to the queue the
    /// window fills in piece by piece over seconds. (vatSys' own controls call Refresh() for the
    /// same reason.)
    /// </summary>
    void FlushPaint(bool everything = false)
    {
        const uint RDW_INVALIDATE = 0x0001, RDW_ALLCHILDREN = 0x0080, RDW_UPDATENOW = 0x0100;
        if (!IsHandleCreated || IsDisposed || !Visible)
        {
            return;
        }
        RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero, RDW_ALLCHILDREN | RDW_UPDATENOW | (everything ? RDW_INVALIDATE : 0));
    }

    // =====================================================================
    //  Title bar: [MOVE]   FLIGHT PLAN WINDOW   [CLOSE]
    // =====================================================================

    protected override void OnTitleBarPaint(NonClientPaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Rectangle area = e.Bounds;
        g.Clear(EcTheme.Background);

        int boxWidth = (int)Math.Ceiling(g.MeasureString("CLOSE", _font).Width) + 22;
        var bar = new Rectangle(area.Left, area.Top, area.Width, area.Height - 1);
        var moveBox = new Rectangle(bar.Left, bar.Top, boxWidth, bar.Height);
        _closeBox = new Rectangle(bar.Right - boxWidth, bar.Top, boxWidth, bar.Height);

        EcPaint.Outline(g, bar, EcTheme.Title);
        EcPaint.FlatBox(g, moveBox, "MOVE", _font, false);
        EcPaint.FlatBox(g, _closeBox, "CLOSE", _font, _closePressed);
        EcPaint.CentredText(g, Text, _font, EcTheme.Title,
            new Rectangle(moveBox.Right, bar.Top, _closeBox.Left - moveBox.Right, bar.Height));
    }

    protected override int OnNonClientAreaHitTest(Point p)
    {
        if (_closeBox.Contains(p))
        {
            // Not caption: clicking CLOSE must not start a window drag. (MOVE is caption, so it drags.)
            return 18;
        }
        return base.OnNonClientAreaHitTest(p);
    }

    protected override void OnNonClientMouseDown(BaseMouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _closeBox.Contains(e.Location))
        {
            _closePressed = true;
            InvalidateNonClient();
            e.Handled = true;
        }
        base.OnNonClientMouseDown(e);
    }

    protected override void OnNonClientMouseUp(BaseMouseEventArgs e)
    {
        bool wasPressed = _closePressed;
        _closePressed = false;
        if (wasPressed)
        {
            InvalidateNonClient();
            if (_closeBox.Contains(e.Location))
            {
                Close();
                return;
            }
        }
        base.OnNonClientMouseUp(e);
    }

    protected override void OnNonClientMouseLeave(EventArgs e)
    {
        if (_closePressed)
        {
            _closePressed = false;
            InvalidateNonClient();
        }
        base.OnNonClientMouseLeave(e);
    }

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    /// <summary>LOWER: drop this window behind vatSys' other windows, as the stock send-to-back button does.</summary>
    void LowerWindow()
    {
        const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;
        var main = FlightPlanInternals.MainForm;
        if (main != null && main.IsHandleCreated)
        {
            SetWindowPos(Handle, main.Handle, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        else
        {
            SendToBack();
        }
    }

    // =====================================================================
    //  Layout
    // =====================================================================

    void BuildLayout()
    {
        int y = Pad;

        Panel grid = BuildButtonGrid();
        Panel row1 = BuildSystemRow();
        Panel row2 = BuildFlightRow();
        Panel row5 = BuildEquipmentRow();

        // The ACID row sits slightly indented from the rows above and below it.
        const int flightRowIndent = 16;
        int width = new[] { grid.Width, row1.Width, row2.Width + flightRowIndent, row5.Width }.Max();

        grid.Location = new Point(Pad, y);
        Controls.Add(grid);
        y = grid.Bottom + 6;

        var separator = new EcSeparator
        {
            Location = new Point(0, y),
            Width = width + Pad * 2
        };
        Controls.Add(separator);
        y = separator.Bottom + 6;

        row1.Location = new Point(Pad, y);
        Controls.Add(row1);
        y = row1.Bottom + 2;

        row2.Location = new Point(Pad + flightRowIndent, y);
        Controls.Add(row2);
        y = row2.Bottom + 6;

        // ---- RTE ----------------------------------------------------------
        const int leftCol = 54;
        const int scrollW = 13;
        int lineH = TextRenderer.MeasureText("W", _font).Height;
        int wideW = width - leftCol - scrollW - 4;

        _rteButton = MakeButton("RTE", 44, 26);
        _rteButton.Location = new Point(Pad, y + 2);
        _rteButton.Click += (s, e) => Guard(ToggleGraphicRoute);
        Controls.Add(_rteButton);

        _rteField = MakeMultilineField(wideW, lineH * 3 + 12);
        _rteField.Location = new Point(Pad + leftCol, y);
        Controls.Add(_rteField);
        AddScrollBar(_rteField, Pad + leftCol + wideW + 4);
        y = _rteField.Bottom + 4;

        // ---- OTHER --------------------------------------------------------
        _otherField = MakeMultilineField(wideW, lineH * 2 + 12);
        _otherField.Location = new Point(Pad + leftCol, y);
        EcLabel otherLabel = MakeLabel("OTHER");
        otherLabel.Location = new Point(Pad, y + 6);
        Controls.Add(otherLabel);
        Controls.Add(_otherField);
        AddScrollBar(_otherField, Pad + leftCol + wideW + 4);
        y = _otherField.Bottom + 4;

        row5.Location = new Point(Pad, y);
        Controls.Add(row5);
        y = row5.Bottom + 8;

        // ---- message area ---------------------------------------------------
        _errorPanel = new Panel
        {
            Location = new Point(Pad, y),
            Size = new Size(width, lineH * 2 + 10),
            BackColor = BackColor
        };
        _errorLabel = new EcLabel("", _font)
        {
            Dock = DockStyle.Fill,
            Visible = false
        };
        _errorPanel.Controls.Add(_errorLabel);
        Controls.Add(_errorPanel);
        y = _errorPanel.Bottom + 6;

        // ---- bottom row -----------------------------------------------------
        var bottomSeparator = new EcSeparator
        {
            Location = new Point(0, y),
            Width = width + Pad * 2
        };
        Controls.Add(bottomSeparator);
        y = bottomSeparator.Bottom + 8;

        _clearButton = MakeButton("Clear", 64, 26);
        _clearButton.Location = new Point(Pad + 6, y);
        _clearButton.Click += (s, e) => Guard(ClearAll);

        _tpcfButton = MakeButton("TPCF", 64, 26);
        _tpcfButton.Location = new Point(Pad + 6 + 64 + 90, y);
        _tpcfButton.Enabled = false;

        _escapeButton = MakeButton("Escape", 64, 26);
        _escapeButton.Location = new Point(Pad + width - 6 - 64, y);
        _escapeButton.Click += (s, e) => Guard(EscapePressed);

        _enterButton = MakeButton("Enter", 64, 26);
        _enterButton.Location = new Point(_escapeButton.Left - 8 - 64, y);
        _enterButton.Click += (s, e) => Guard(EnterPressed);

        Controls.AddRange(new Control[] { _clearButton, _tpcfButton, _enterButton, _escapeButton });
        y = _clearButton.Bottom + 10;

        // ---- footer: [LOWER] ------------------------------------------------
        const int footerH = 22;
        var footerLine = new Panel
        {
            BackColor = EcTheme.Title,
            Location = new Point(0, y),
            Size = new Size(width + Pad * 2, 1)
        };
        Controls.Add(footerLine);

        _lowerButton = MakeButton("LOWER", TextRenderer.MeasureText("CLOSE", _font).Width + 16, footerH);
        _lowerButton.Flat = true;
        _lowerButton.Location = new Point(0, y + 1);
        _lowerButton.Click += (s, e) => Guard(LowerWindow);
        Controls.Add(_lowerButton);
        y += footerH + 1;

        ClientSize = new Size(width + Pad * 2, y);

        _planFields = new[]
        {
            _frulField, _typ1Field, _typ2Field, _typ3Field, _adepField, _etdField, _adesField, _rflField,
            _rteField, _otherField, _equip1Field, _equip2Field, _tasField, _eetField, _altn1Field, _regnField
        };

        // Keyboard order follows the form top-to-bottom, left-to-right. Fields sit inside row
        // panels, so the rows are ordered first and the fields within each row second.
        int tab = 0;
        foreach (Control c in new Control[] { row1, row2, _rteField, _otherField, row5 })
        {
            c.TabIndex = tab++;
        }
    }

    Panel BuildButtonGrid()
    {
        var panel = new Panel();

        // Column x-positions: groups are separated by a wider gap.
        int[] groupSizes = { 1, 4, 3, 1, 2 };
        var colX = new List<int>();
        int x = 0;
        foreach (int size in groupSizes)
        {
            for (int i = 0; i < size; i++)
            {
                colX.Add(x);
                x += BtnW + BtnGap;
            }
            x += GroupGap - BtnGap;
        }
        int timeX = x;

        Point Cell(int col, int row) => new Point(colX[col], row * (BtnH + BtnGap));

        EcButton Add(string text, int col, int row, Action? onClick = null)
        {
            EcButton b = MakeButton(text, BtnW, BtnH);
            b.Location = Cell(col, row);
            if (onClick != null)
            {
                b.Click += (s, e) => Guard(onClick);
            }
            else
            {
                // No vatSys equivalent - shown for layout fidelity only.
                b.Enabled = false;
            }
            panel.Controls.Add(b);
            return b;
        }

        EcButton AddToggle(string text, int col, int row, EventHandler onChanged)
        {
            EcButton b = MakeButton(text, BtnW, BtnH);
            b.Toggle = true;
            b.Location = Cell(col, row);
            b.CheckedChanged += onChanged;
            panel.Controls.Add(b);
            return b;
        }

        // group 0: paging
        _nextButton = Add("NEXT", 0, 0);
        _prevButton = Add("PREV", 0, 1);
        _counterLabel = MakeLabel("0/0", BtnW, BtnH);
        _counterLabel.Location = Cell(0, 2);
        panel.Controls.Add(_counterLabel);

        // group 1: plan handling
        _creButton = AddToggle("CRE", 1, 0, CreButton_CheckedChanged);
        _rplButton = Add("RPL", 2, 0);
        _estButton = Add("EST", 3, 0, EstClicked);
        _holdButton = Add("HOLD", 4, 0);
        _crteButton = Add("CRTE", 1, 1);
        _steButton = Add("STE", 2, 1);
        _modButton = AddToggle("MOD", 3, 1, ModButton_CheckedChanged);
        _backButton = Add("BACK", 4, 1, BackClicked);
        _stateLabel = MakeLabel("", BtnW, BtnH);
        _stateLabel.Location = Cell(1, 2);
        panel.Controls.Add(_stateLabel);
        _prlButton = Add("PRL", 2, 2, PrlClicked);
        _tmaButton = Add("TMA", 3, 2);
        _cflButton = Add("CFL", 4, 2, CflClicked);

        // group 2: control
        _depButton = AddToggle("DEP", 5, 0, DepButton_CheckedChanged);
        _accButton = Add("ACC", 6, 0, AccClicked);
        _cnlButton = Add("CNL", 7, 0, CnlClicked);
        _cbepButton = Add("CBEP", 5, 1);
        _hndButton = Add("HND", 6, 1, HndClicked);
        _inhiButton = Add("INHI", 7, 1, InhiClicked);
        _exitButton = Add("EXIT", 5, 2);
        _hndiButton = Add("HND/I", 6, 2);
        _supButton = Add("SUP", 7, 2);

        // group 3: coupling
        _ncouButton = Add("NCOU", 8, 0, NcouClicked);
        _bcouButton = Add("BCOU", 8, 1, BcouClicked);

        // group 4: misc (no vatSys equivalent)
        _trsButton = Add("TRS", 9, 0);
        _jtrtButton = Add("JTRT", 10, 0);
        _aurButton = Add("AUR", 9, 1);
        _printStripButton = Add("PRINT\nSTRIP", 10, 1);
        _cmcButton = Add("CMC", 9, 2);
        _fmfButton = Add("FMF", 10, 2);

        // update time column
        EcLabel updateCaption = MakeLabel("Update Time", TimeColW, BtnH / 2 + 4);
        updateCaption.Location = new Point(timeX, 2);
        panel.Controls.Add(updateCaption);

        _updateTimeLabel = MakeLabel("00:00:00", TimeColW, BtnH / 2 + 4);
        _updateTimeLabel.Location = new Point(timeX, updateCaption.Bottom);
        panel.Controls.Add(_updateTimeLabel);

        _printFdrButton = MakeButton("PRINT\nFDR", BtnW, BtnH);
        _printFdrButton.Location = new Point(timeX + (TimeColW - BtnW) / 2, 2 * (BtnH + BtnGap));
        _printFdrButton.Enabled = false;
        panel.Controls.Add(_printFdrButton);

        panel.Size = new Size(timeX + TimeColW, 3 * BtnH + 2 * BtnGap);
        return panel;
    }

    Panel BuildSystemRow()
    {
        var row = new RowBuilder(this);
        _lplField = row.Field("LPL", 3);
        _csecField = row.Field("CSEC", 4);
        _pssrField = row.Field("PSSR", 4);
        _assrField = row.Field("ASSR", 4, maxLength: 4);
        _assrField.OctalOnly = true;
        _opdataField = row.Field("OPDATA", 10);
        _atdField = row.Field("ATD", 4, maxLength: 4, numeric: true);
        return row.Finish();
    }

    Panel BuildFlightRow()
    {
        var row = new RowBuilder(this);
        _acidField = row.Field("ACID", 7, maxLength: 7);
        _frulField = row.Field("FRUL", 1, maxLength: 1);
        _typ1Field = row.Field("TYP", 2, maxLength: 2, numeric: true);
        _typ2Field = row.Field("/", 4, maxLength: 4, tight: true);
        _typ3Field = row.Field("/", 1, maxLength: 1, tight: true);
        _adepField = row.Field("ADEP", 4, maxLength: 4);
        _etdField = row.Field("ETD", 4, maxLength: 4, numeric: true);
        _adesField = row.Field("ADES", 4, maxLength: 4);
        _rflField = row.Field("RFL", 3, maxLength: 3);
        return row.Finish();
    }

    Panel BuildEquipmentRow()
    {
        var row = new RowBuilder(this);
        _ftypField = row.Field("FTYP", 1, maxLength: 1);
        _equip1Field = row.Field("EQUIP", 10);
        _equip2Field = row.Field("/", 4, tight: true);
        _tasField = row.Field("TAS", 4, maxLength: 4, numeric: true);
        _eetField = row.Field("EET", 4, maxLength: 4, numeric: true);
        _altn1Field = row.Field("ALTN", 4, maxLength: 4);
        _altn2Field = row.Field(null, 4, maxLength: 4, tight: true);
        _regnField = row.Field("REGN", 7, maxLength: 7);
        return row.Finish();
    }

    /// <summary>Lays out "LABEL [field]" pairs left to right in an auto-sized panel.</summary>
    sealed class RowBuilder
    {
        readonly FlightPlanWindow owner;
        readonly Panel panel = new Panel();
        int x;
        int height;

        public RowBuilder(FlightPlanWindow owner)
        {
            this.owner = owner;
        }

        public EcField Field(string? label, int chars, int maxLength = 0, bool numeric = false, bool tight = false)
        {
            var field = new EcField(owner._font) { NumericOnly = numeric };
            if (maxLength > 0)
            {
                field.MaxLength = maxLength;
            }
            field.SizeForChars(chars);
            height = Math.Max(height, field.Height);

            if (x > 0)
            {
                x += tight ? 0 : 8;
            }

            if (label is { Length: > 0 })
            {
                EcLabel l = owner.MakeLabel(label);
                l.Location = new Point(x, 0);
                panel.Controls.Add(l);
                x += l.Width;
            }

            field.Location = new Point(x, 0);
            field.TabIndex = panel.Controls.Count;
            panel.Controls.Add(field);
            x += field.Width;
            return field;
        }

        public Panel Finish()
        {
            foreach (Control c in panel.Controls)
            {
                if (c is EcLabel)
                {
                    c.Height = height;
                }
            }
            panel.Size = new Size(x, height);
            return panel;
        }
    }

    EcLabel MakeLabel(string text, int width = 0, int height = 0)
    {
        Size measured = TextRenderer.MeasureText(text.Length == 0 ? "W" : text, _font);
        return new EcLabel(text, _font)
        {
            Size = new Size(width > 0 ? width : measured.Width, height > 0 ? height : measured.Height + 4)
        };
    }

    EcButton MakeButton(string text, int width, int height)
    {
        return new EcButton
        {
            Text = text,
            Font = _font,
            Size = new Size(width, height)
        };
    }

    EcField MakeMultilineField(int width, int height)
    {
        return new EcField(_font, multiline: true)
        {
            Size = new Size(width, height)
        };
    }

    /// <summary>Scroll strip to the right of a multi-line field, inset to line up with the field's box.</summary>
    void AddScrollBar(EcField target, int x)
    {
        Controls.Add(new EcScrollBar(target)
        {
            Location = new Point(x, target.Top + 2),
            Size = new Size(13, target.Height - 4)
        });
    }

    // =====================================================================
    //  Loading / state
    // =====================================================================

    /// <summary>
    /// Shows the flight plan, or clears the window if <paramref name="fdr"/> is <c>null</c>.
    /// </summary>
    public void LoadFDR(FDP2.FDR? fdr)
    {
        Unsubscribe();
        HideGraphicRoute();
        CancelPendingRequest();

        _fdr = fdr;
        _mode = Mode.View;
        if (_fdr != null)
        {
            _acidField.Text = _fdr.Callsign;
            _fdr.PropertyChanged += Fdr_PropertyChanged;
        }

        LoadFieldData(false);
        StampUpdateTime();
        RefreshState();
    }

    void Unsubscribe()
    {
        if (_fdr != null)
        {
            _fdr.PropertyChanged -= Fdr_PropertyChanged;
        }
    }

    /// <summary>Copies FDR data into the fields. When <paramref name="keepEdits"/> is set, fields the user is typing in are left alone.</summary>
    void LoadFieldData(bool keepEdits)
    {
        void Set(EcField field, string value)
        {
            if (keepEdits && field.Editable && field.Modified)
            {
                return;
            }
            if (field.Text != (value ?? ""))
            {
                field.Text = value ?? "";
            }
            field.Modified = false;
        }

        var f = _fdr;
        if (f == null)
        {
            foreach (EcField field in AllFieldsExceptAcid())
            {
                Set(field, "");
            }
            return;
        }

        Set(_frulField, f.FlightRules);
        Set(_typ1Field, f.AircraftCount.ToString());
        Set(_typ2Field, f.AircraftType);
        Set(_typ3Field, f.AircraftWake);
        Set(_equip1Field, f.AircraftEquip);
        Set(_equip2Field, f.AircraftSurvEquip);
        Set(_adepField, f.DepAirport);
        Set(_etdField, f.ETD.ToString("HHmm"));
        Set(_tasField, f.TAS.ToString());
        Set(_rflField, (f.RFL / 100).ToString("D3"));
        Set(_adesField, f.DesAirport);
        Set(_eetField, f.EET.ToString("hhmm"));
        Set(_altn1Field, f.AltAirport);
        Set(_altn2Field, "");
        Set(_rteField, f.Route);
        Set(_otherField, f.Remarks);
        Set(_regnField, ParseRegistration(f.Remarks));
        Set(_ftypField, "");
        Set(_lplField, "");
        Set(_assrField, f.AssignedSSRCode != -1 && SquawkAssignment.IsCodeDiscrete(f.AssignedSSRCode)
            ? Convert.ToString(f.AssignedSSRCode, 8).PadLeft(4, '0')
            : "");
        Set(_opdataField, f.LabelOpData);
        Set(_atdField, f.ATD != DateTime.MaxValue ? f.ATD.ToString("HHmm") : "");

        string sector = f.ControllingSector?.Name ?? "";
        Set(_csecField, sector.Length > 3 ? sector.Substring(0, 3) + "*" : sector);

        string squawk = "";
        try
        {
            var pilot = f.CoupledTrack?.ActualAircraft;
            if (pilot != null)
            {
                squawk = Convert.ToString(pilot.TransponderCode, 8).PadLeft(4, '0');
            }
        }
        catch
        {
        }
        Set(_pssrField, squawk);
    }

    IEnumerable<EcField> AllFieldsExceptAcid()
    {
        return new[]
        {
            _lplField, _csecField, _pssrField, _assrField, _opdataField, _atdField, _frulField, _typ1Field, _typ2Field, _typ3Field,
            _adepField, _etdField, _adesField, _rflField, _rteField, _otherField, _ftypField, _equip1Field, _equip2Field, _tasField,
            _eetField, _altn1Field, _altn2Field, _regnField
        };
    }

    void StampUpdateTime()
    {
        _updateTimeLabel.Text = _fdr == null ? "00:00:00" : DateTime.UtcNow.ToString("HH:mm:ss");
    }

    static string StateText(States state)
    {
        switch (state)
        {
            case States.STATE_INACTIVE: return "NACT";
            case States.STATE_FINISHED: return "FIN";
            case States.STATE_HANDOVER:
            case States.STATE_HANDOVER_FIRST: return "HND";
            case States.STATE_INHIBITED: return "INHI";
            case States.STATE_SUSPENDED: return "SUS";
            case States.STATE_UNCONTROLLED: return "UCTL";
            case States.STATE_PREACTIVE: return "PREA";
            case States.STATE_COORDINATED: return "COOR";
            case States.STATE_CONTROLLED: return "CONT";
            default: return "";
        }
    }

    /// <summary>Whether the label/system data (ASSR, OPDATA, PRL, CFL) may be edited - mirrors the stock window's "system" button.</summary>
    bool CanEditSystemData()
    {
        if (_fdr == null || !_fdr.HavePermission)
        {
            return false;
        }
        switch (_fdr.State)
        {
            case States.STATE_HANDOVER:
            case States.STATE_HANDOVER_FIRST:
            case States.STATE_INHIBITED:
            case States.STATE_PREACTIVE:
            case States.STATE_COORDINATED:
            case States.STATE_CONTROLLED:
                return true;
            default:
                return false;
        }
    }

    /// <summary>Single place that derives every enabled/checked state from (mode, FDR, network).</summary>
    void RefreshState()
    {
        _updating = true;
        try
        {
            var fdr = _fdr;
            bool has = fdr != null;
            bool valid = Network.ValidATC;
            bool perm = fdr != null && fdr.HavePermission;
            States state = fdr?.State ?? States.STATE_NULL;
            bool view = _mode == Mode.View && !_requestPending;

            bool canCreate = valid;
            bool canMod = false, canCnl = false, canEst = false, canBack = false, canDep = false;
            bool canAcc = false, canHnd = false, canInhi = false;

            if (fdr != null)
            {
                bool own = valid && (perm || !fdr.IsTracked);
                canCreate = own;
                canMod = own;
                canCnl = own;

                switch (state)
                {
                    case States.STATE_INACTIVE:
                        canEst = true;
                        canCnl = false;
                        break;
                    case States.STATE_FINISHED:
                        canMod = false;
                        canCnl = false;
                        break;
                    case States.STATE_HANDOVER:
                    case States.STATE_HANDOVER_FIRST:
                        canAcc = valid;
                        canCreate = false;
                        canInhi = perm;
                        break;
                    case States.STATE_INHIBITED:
                        canInhi = true;
                        canAcc = valid;
                        canCreate = false;
                        break;
                    case States.STATE_SUSPENDED:
                        canCreate = false;
                        break;
                    case States.STATE_UNCONTROLLED:
                        canAcc = valid;
                        canCreate = false;
                        break;
                    case States.STATE_PREACTIVE:
                        canEst = true;
                        canCreate = false;
                        break;
                    case States.STATE_COORDINATED:
                        canDep = true;
                        canBack = fdr.ATD == DateTime.MaxValue;
                        canCreate = false;
                        canInhi = perm;
                        break;
                    case States.STATE_CONTROLLED:
                        canDep = perm;
                        canHnd = perm;
                        canCreate = false;
                        canInhi = perm;
                        break;
                }
            }

            bool sys = CanEditSystemData();

            // ---- function buttons ----
            _creButton.Checked = _mode == Mode.Create;
            _modButton.Checked = _mode == Mode.Modify;
            _depButton.Checked = _mode == Mode.Depart;

            _creButton.Enabled = _mode == Mode.Create || (view && canCreate);
            _modButton.Enabled = _mode == Mode.Modify || (view && canMod);
            _depButton.Enabled = _mode == Mode.Depart || (view && canDep);

            _estButton.Enabled = view && canEst;
            _backButton.Enabled = view && canBack;
            _cnlButton.Enabled = view && canCnl;
            _accButton.Enabled = view && canAcc;
            _hndButton.Enabled = view && canHnd;
            _inhiButton.Enabled = view && canInhi && valid;
            _prlButton.Enabled = view && sys;
            _cflButton.Enabled = view && sys;
            _rteButton.Enabled = has && _mode == Mode.View;
            _enterButton.Enabled = !_requestPending;

            UpdateCoupleButtons(view);

            // ---- fields ----
            bool editingPlan = _mode == Mode.Create || _mode == Mode.Modify;
            foreach (EcField field in _planFields)
            {
                SetFieldEnabled(field, editingPlan);
            }

            SetFieldEnabled(_acidField, _mode == Mode.View);
            SetFieldEnabled(_atdField, _mode == Mode.Depart);
            SetFieldEnabled(_assrField, (view && sys) || _mode == Mode.Create);
            SetFieldEnabled(_opdataField, (view && sys) || _mode == Mode.Create);

            // Display-only (no vatSys data behind them, or derived).
            SetFieldEnabled(_lplField, false);
            SetFieldEnabled(_csecField, false);
            SetFieldEnabled(_pssrField, false);
            SetFieldEnabled(_ftypField, false);
            SetFieldEnabled(_altn2Field, false);

            _stateLabel.Text = has ? StateText(state) : "";
            _counterLabel.Text = has ? "1/1" : "0/0";
        }
        finally
        {
            _updating = false;
            FlushPaint();
        }
    }

    void SetFieldEnabled(EcField field, bool enabled)
    {
        // Editable fields draw as sunken boxes, display-only ones as a thin outline.
        field.Editable = enabled;
    }

    void UpdateCoupleButtons(bool view)
    {
        bool canDecouple = false;
        bool canCouple = false;

        if (_fdr != null && view)
        {
            if (_fdr.CoupledTrack != null)
            {
                canDecouple = true;
            }
            else
            {
                Track selected = MMI.SelectedTrack;
                bool selectedIsFreeRadarTrack = selected != null
                                                && selected.Type == Track.TrackTypes.TRACK_TYPE_RADAR
                                                && selected.GetFDR() == null;
                canCouple = selectedIsFreeRadarTrack || _fdr.InhibitAutoCouple;
            }
        }

        _ncouButton.Enabled = canDecouple;
        _bcouButton.Enabled = canCouple;
    }

    // =====================================================================
    //  vatSys events
    // =====================================================================

    void OnGui(Action action)
    {
        if (IsDisposed)
        {
            return;
        }
        if (IsHandleCreated && InvokeRequired)
        {
            BeginInvoke((MethodInvoker)(() =>
            {
                if (!IsDisposed)
                {
                    action();
                }
            }));
        }
        else
        {
            action();
        }
    }

    void Fdr_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != null && !WatchedProperties.Contains(e.PropertyName))
        {
            return;
        }

        OnGui(() =>
        {
            if (_fdr == null || sender != _fdr || _mode != Mode.View)
            {
                return;
            }
            LoadFieldData(true);
            StampUpdateTime();
            RefreshState();
        });
    }

    void FDP2_FDRsChanged(object sender, FDP2.FDRsChangedEventArgs e)
    {
        var current = _fdr;
        if (current == null || e.Change != current || FDP2.GetFDRs.Contains(current))
        {
            return;
        }

        // Our FDR was deleted.
        OnGui(() =>
        {
            if (_fdr == current)
            {
                LoadFDR(null);
            }
        });
    }

    void Network_ValidATCChanged(object sender, EventArgs e)
    {
        OnGui(() => LoadFDR(null));
    }

    void MMI_SelectedTrackChanged(object sender, EventArgs e)
    {
        if (_fdr == null)
        {
            return;
        }
        OnGui(() => UpdateCoupleButtons(_mode == Mode.View && !_requestPending));
    }

    // =====================================================================
    //  Mode buttons
    // =====================================================================

    void CreButton_CheckedChanged(object sender, EventArgs e)
    {
        if (_updating)
        {
            return;
        }

        Guard(() =>
        {
            if (!_creButton.Checked)
            {
                LeaveEditMode();
                return;
            }

            string acid = _acidField.Text.Trim().ToUpperInvariant();
            if (acid == "")
            {
                ShowError(new FDRParseException(FDRParseException.NOT_VALID_CALLSIGN, _acidField.Text));
                RefreshState();
                return;
            }

            bool sameFlight = _fdr?.Callsign == acid;
            if (sameFlight && _fdr?.HavePermission == false)
            {
                ShowError(new FDRParseException(FDRParseException.CONT_FP_EXISTS, acid));
                RefreshState();
                return;
            }

            List<NetworkPilot> pilots = Network.GetOnlinePilots;
            if (pilots == null || !pilots.Any(p => (p.Callsign ?? "").ToUpperInvariant().Trim() == acid))
            {
                ShowError(new FDRParseException(FDRParseException.CALLSIGN_DOES_NOT_EXIST, acid));
                RefreshState();
                return;
            }

            if (!sameFlight && _fdr != null)
            {
                // A different callsign was typed over a loaded plan: start from blank rather than
                // silently copying the other aircraft's plan.
                Unsubscribe();
                HideGraphicRoute();
                _fdr = null;
                LoadFieldData(false);
                StampUpdateTime();
            }

            _acidField.Text = acid;
            _mode = Mode.Create;
            ClearError();
            RefreshState();

            _etdField.Text = "";
            _assrField.Text = "";
            _opdataField.Text = "";
            _atdField.Text = "";
            SetDefault(_typ1Field, "1");
            SetDefault(_frulField, "I");
            SetDefault(_equip1Field, "G");
            SetDefault(_equip2Field, "C");
            _typ2Field.Focus();
        });
    }

    static void SetDefault(EcField field, string value)
    {
        if (field.Text.Trim() == "" || field.Text.Trim() == "0")
        {
            field.Text = value;
        }
    }

    void ModButton_CheckedChanged(object sender, EventArgs e)
    {
        if (_updating)
        {
            return;
        }

        Guard(() =>
        {
            if (!_modButton.Checked)
            {
                LeaveEditMode();
                return;
            }

            if (_fdr == null)
            {
                RefreshState();
                return;
            }
            if (!_fdr.HavePermission)
            {
                ShowError(new FDRParseException(FDRParseException.CONT_FP_EXISTS, _fdr.Callsign));
                RefreshState();
                return;
            }

            _acidField.Text = _fdr.Callsign;
            LoadFieldData(false);
            _mode = Mode.Modify;
            ClearError();
            RefreshState();
            _rteField.Focus();
        });
    }

    void DepButton_CheckedChanged(object sender, EventArgs e)
    {
        if (_updating)
        {
            return;
        }

        Guard(() =>
        {
            if (!_depButton.Checked)
            {
                LeaveEditMode();
                return;
            }

            if (_fdr == null)
            {
                RefreshState();
                return;
            }

            _mode = Mode.Depart;
            ClearError();
            RefreshState();
            _atdField.Focus();
            _atdField.SelectAll();
        });
    }

    /// <summary>Abandons Create/Modify/Depart and shows the stored plan again.</summary>
    void LeaveEditMode()
    {
        ClearError();
        LoadFDR(_fdr != null && FDP2.GetFDRs.Contains(_fdr) ? _fdr : null);
    }

    // =====================================================================
    //  Enter / Escape / Clear
    // =====================================================================

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // The buttons never take focus, so Enter/Escape are routed here rather than through
        // AcceptButton/CancelButton.
        if (keyData == Keys.Return)
        {
            if (_enterButton.Enabled)
            {
                Guard(EnterPressed);
            }
            return true;
        }
        if (keyData == Keys.Escape)
        {
            Guard(EscapePressed);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    void EnterPressed()
    {
        switch (_mode)
        {
            case Mode.Modify:
                CommitModify();
                break;
            case Mode.Create:
                CommitCreate();
                break;
            case Mode.Depart:
                CommitDepart();
                break;
            default:
                EnterInViewMode();
                break;
        }
    }

    void EscapePressed()
    {
        ClearError();
        if (_mode != Mode.View)
        {
            LeaveEditMode();
        }
        else
        {
            string acid = _acidField.Text;
            LoadFDR(null);
            _acidField.Text = acid;
        }
        _acidField.Focus();
    }

    void ClearAll()
    {
        ClearError();
        LoadFDR(null);
        _acidField.Text = "";
        _acidField.Focus();
    }

    static string OneLine(string text)
    {
        return Regex.Replace(text ?? "", @"\s*[\r\n]+\s*", " ").Trim();
    }

    void CommitModify()
    {
        var fdr = _fdr;
        if (fdr == null)
        {
            LeaveEditMode();
            return;
        }
        if (!fdr.HavePermission)
        {
            ShowError(new FDRParseException(FDRParseException.CONT_FP_EXISTS, "CSEC " + fdr.ControllerTracking?.Callsign));
            return;
        }

        try
        {
            FDP2.ModifyFDR(fdr, fdr.Callsign, _frulField.Text, _adepField.Text, _adesField.Text, OneLine(_rteField.Text),
                RemarksWithRegistration(), _typ1Field.Text, _typ2Field.Text, _typ3Field.Text, _equip1Field.Text, _equip2Field.Text,
                _tasField.Text, _rflField.Text, _etdField.Text, _eetField.Text, _atdField.Text, _altn1Field.Text);
        }
        catch (FDRParseException ex)
        {
            ShowError(ex);
            return;
        }

        FlightPlanInternals.SendFlightPlanChange(fdr);
        ClearError();
        LoadFDR(fdr);
    }

    void CommitCreate()
    {
        string acid = _acidField.Text.Trim().ToUpperInvariant();
        bool existed = FDP2.GetFDRIndex(acid) != -1;

        try
        {
            FDP2.CreateFDR(acid, _frulField.Text, _adepField.Text, _adesField.Text, OneLine(_rteField.Text), RemarksWithRegistration(),
                _typ1Field.Text, _typ2Field.Text, _typ3Field.Text, _equip1Field.Text, _equip2Field.Text, _tasField.Text, _rflField.Text,
                _etdField.Text, _eetField.Text, "", _altn1Field.Text);
        }
        catch (FDRParseException ex)
        {
            ShowError(ex);
            return;
        }

        FDP2.FDR created = FDP2.GetFDRs.LastOrDefault(f => f.Callsign == acid);
        if (created == null)
        {
            ShowError("'" + acid + "' : FDR was not created");
            return;
        }

        if (existed)
        {
            FlightPlanInternals.SendFlightPlanChange(created);
        }
        else
        {
            FlightPlanInternals.SendNewFlightPlan(created);
        }

        // ASSR / OPDATA typed while creating are applied to the new plan.
        string? problem = null;
        string ssr = _assrField.Text.Trim();
        string opdata = _opdataField.Text.Trim();
        if (ssr != "")
        {
            try
            {
                FDP2.SetASSR(created, Convert.ToInt32(ssr, 8));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is OverflowException)
            {
                problem = ex is ArgumentException ? ex.Message : "Invalid SSR code";
            }
        }
        if (opdata != "")
        {
            FDP2.SetLabelData(created, opdata);
        }

        ClearError();
        LoadFDR(created);
        if (problem != null)
        {
            ShowError(problem);
        }
    }

    void CommitDepart()
    {
        var fdr = _fdr;
        if (fdr == null)
        {
            LeaveEditMode();
            return;
        }

        try
        {
            if (_atdField.Text.Length == 0)
            {
                if (fdr.ATD != DateTime.MaxValue)
                {
                    FDP2.CancelFDRDeparture(fdr);
                }
                else
                {
                    FDP2.DepartFDR(fdr, DateTime.UtcNow);
                }
            }
            else
            {
                FDP2.DepartFDR(fdr, _atdField.Text);
            }
        }
        catch
        {
            ShowError("Unable to parse ATD");
            return;
        }

        ClearError();
        LoadFDR(fdr);
    }

    void EnterInViewMode()
    {
        string acid = _acidField.Text.Trim().ToUpperInvariant();
        if (acid == "")
        {
            return;
        }

        if (_fdr != null && _fdr.Callsign == acid)
        {
            if (CanEditSystemData())
            {
                CommitSystemData();
            }
            return;
        }

        int index = FDP2.GetFDRIndex(acid);
        if (index != -1)
        {
            FDP2.FDR found = FDP2.GetFDRs.FirstOrDefault(f => f.Callsign == acid);
            if (found != null)
            {
                ClearError();
                LoadFDR(found);
                return;
            }
        }

        RequestFlightPlan(acid);
    }

    /// <summary>Applies ASSR / OPDATA edits made directly in the window.</summary>
    void CommitSystemData()
    {
        var fdr = _fdr;
        if (fdr == null)
        {
            return;
        }

        if (_assrField.Modified)
        {
            string text = _assrField.Text.Trim();
            if (text.Length == 0)
            {
                FDP2.SetASSR(fdr);
            }
            else
            {
                int code;
                try
                {
                    code = Convert.ToInt32(text, 8);
                }
                catch
                {
                    ShowError("Invalid SSR code");
                    return;
                }

                if (fdr.AssignedSSRCode != code)
                {
                    try
                    {
                        FDP2.SetASSR(fdr, code);
                    }
                    catch (ArgumentException ex)
                    {
                        ShowError(ex.Message);
                        return;
                    }
                }
            }
        }

        if (_opdataField.Modified && _opdataField.Text != fdr.LabelOpData)
        {
            FDP2.SetLabelData(fdr, _opdataField.Text);
        }

        ClearError();
        _assrField.Modified = false;
        _opdataField.Modified = false;
        LoadFieldData(false);
        StampUpdateTime();
        RefreshState();
    }

    /// <summary>No local FDR: ask the network for the plan, like the stock window does.</summary>
    void RequestFlightPlan(string acid)
    {
        CancelPendingRequest();
        Unsubscribe();
        HideGraphicRoute();
        _fdr = null;
        LoadFieldData(false);
        ClearError();

        _requestPending = true;
        RefreshState();

        _requestTimer = new Timer { Interval = 3000 };
        _requestTimer.Tick += (s, e) => FinishRequest(acid, false);

        try
        {
            _unsubscribeNoFp = FlightPlanInternals.SubscribeNoFlightPlan(callsign =>
            {
                if (callsign != null && callsign.Trim().ToUpperInvariant() == acid)
                {
                    OnGui(() => FinishRequest(acid, true));
                }
            });
        }
        catch (Exception ex)
        {
            // Without the event, the timer still finds that the flight plan did not come
            _errorReporter.ReportError(ex, "Cannot find when the network has no flight plan");
        }

        try
        {
            FlightPlanInternals.SendFlightPlanRequest(acid);
        }
        catch
        {
            CancelPendingRequest();
            RefreshState();
            throw;
        }
        _requestTimer.Start();
    }

    void FinishRequest(string acid, bool definitelyMissing)
    {
        if (!_requestPending)
        {
            return;
        }
        CancelPendingRequest();

        var found = definitelyMissing ? null : FDP2.GetFDRs.FirstOrDefault(f => f.Callsign == acid);
        if (found != null)
        {
            ClearError();
            LoadFDR(found);
        }
        else
        {
            RefreshState();
            ShowError("'" + acid + "' : no FDR exists");
        }
    }

    void CancelPendingRequest()
    {
        _requestPending = false;
        if (_requestTimer != null)
        {
            _requestTimer.Stop();
            _requestTimer.Dispose();
            _requestTimer = null;
        }
        if (_unsubscribeNoFp != null)
        {
            _unsubscribeNoFp();
            _unsubscribeNoFp = null;
        }
    }

    // =====================================================================
    //  Registration (REGN <-> REG/ in remarks)
    // =====================================================================

    static readonly Regex RegRegex = new Regex(@"(?<![A-Z0-9])REG/(\S+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static string ParseRegistration(string remarks)
    {
        Match m = RegRegex.Match(remarks ?? "");
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : "";
    }

    /// <summary>Remarks text with the REG/ item brought in line with the REGN field.</summary>
    internal static string ApplyRegistration(string remarks, string regn)
    {
        remarks = remarks ?? "";
        regn = (regn ?? "").Trim().ToUpperInvariant();
        if (regn == ParseRegistration(remarks))
        {
            return remarks;
        }

        if (regn == "")
        {
            return Regex.Replace(RegRegex.Replace(remarks, ""), @"\s{2,}", " ").Trim();
        }
        if (RegRegex.IsMatch(remarks))
        {
            return RegRegex.Replace(remarks, "REG/" + regn, 1);
        }
        return (remarks.Trim() + " REG/" + regn).Trim();
    }

    string RemarksWithRegistration()
    {
        return ApplyRegistration(OneLine(_otherField.Text), _regnField.Text);
    }

    // =====================================================================
    //  Function buttons
    // =====================================================================

    void EstClicked()
    {
        var fdr = _fdr;
        if (fdr == null)
        {
            return;
        }
        if (fdr.ParsedRoute == null || fdr.ParsedRoute.Count == 0)
        {
            ShowError("Cannot activate (NO ROUTE)");
            return;
        }
        FlightPlanInternals.ShowEstimateWindow(fdr);
        ReloadAfterAction(fdr);
    }

    void BackClicked()
    {
        var fdr = _fdr;
        if (fdr == null)
        {
            return;
        }
        FDP2.BackFDR(fdr);
        if (fdr.ParsedRoute != null && fdr.ParsedRoute.Count > 0)
        {
            FlightPlanInternals.SendCancelEstimate(fdr, fdr.ParsedRoute[0]);
        }
        ReloadAfterAction(fdr);
    }

    void InhiClicked()
    {
        var fdr = _fdr;
        if (fdr == null)
        {
            return;
        }
        if (fdr.State == States.STATE_INHIBITED)
        {
            MMI.UnInhibit(fdr);
        }
        else
        {
            MMI.Inhibit(fdr);
        }
        ReloadAfterAction(fdr);
    }

    void CnlClicked()
    {
        var fdr = _fdr;
        if (fdr == null)
        {
            return;
        }
        if (!fdr.HavePermission && fdr.IsTracked)
        {
            ShowError(new FDRParseException(FDRParseException.CONT_FP_EXISTS, fdr.Callsign));
            return;
        }

        string message = fdr.State <= States.STATE_COORDINATED
            ? "This will deactivate the FDR for " + fdr.Callsign + "."
            : "This will finish the FDR for " + fdr.Callsign + ".";
        if (FlightPlanInternals.Confirm("Cancel " + fdr.Callsign + "?", message))
        {
            FDP2.CancelFDR(fdr, sendEst: true);
        }
        ReloadAfterAction(fdr);
    }

    void AccClicked()
    {
        if (_fdr != null)
        {
            MMI.AcceptJurisdiction(_fdr);
        }
    }

    void HndClicked()
    {
        if (_fdr != null)
        {
            MMI.OpenHandoffMenu(_fdr);
        }
    }

    void PrlClicked()
    {
        var fdr = _fdr;
        if (fdr == null)
        {
            return;
        }
        if (fdr.CoupledTrack != null)
        {
            ShowError("PRL not available (track coupled)");
            return;
        }
        ClearError();
        MMI.OpenPRLMenu(fdr, Cursor.Position);
    }

    void CflClicked()
    {
        var fdr = _fdr;
        if (fdr == null)
        {
            return;
        }
        Track track = MMI.FindTrack(fdr);
        if (track == null)
        {
            ShowError("CFL not available (no track)");
            return;
        }
        ClearError();
        MMI.OpenCFLMenu(track, Cursor.Position);
    }

    /// <summary>NCOU: break the radar coupling and stop auto-coupling.</summary>
    void NcouClicked()
    {
        var fdr = _fdr;
        if (fdr?.CoupledTrack == null)
        {
            return;
        }
        RDP.DeCouple(fdr.CoupledTrack);
        fdr.InhibitAutoCouple = true;
        RefreshState();
    }

    /// <summary>BCOU: couple to the selected radar track, or hand coupling back to the system.</summary>
    void BcouClicked()
    {
        var fdr = _fdr;
        if (fdr == null || fdr.CoupledTrack != null)
        {
            return;
        }

        Track selected = MMI.SelectedTrack;
        if (selected != null && selected.Type == Track.TrackTypes.TRACK_TYPE_RADAR && selected.GetFDR() == null)
        {
            RDP.Couple(fdr, selected.GetRadarTrack());
            fdr.InhibitAutoCouple = true;
            MMI.DeselectTrack();
        }
        else if (fdr.InhibitAutoCouple)
        {
            fdr.InhibitAutoCouple = false;
        }
        else
        {
            ShowError("Invalid / no radar track selected");
        }
        RefreshState();
    }

    void ToggleGraphicRoute()
    {
        if (_routeTrack != null)
        {
            HideGraphicRoute();
            return;
        }

        if (_fdr == null)
        {
            return;
        }
        Track track = MMI.FindTrack(_fdr);
        if (track == null)
        {
            ShowError("No track to display route for");
            return;
        }
        ClearError();
        MMI.ShowGraphicRoute(track);
        _routeTrack = track;
    }

    void HideGraphicRoute()
    {
        if (_routeTrack == null)
        {
            return;
        }
        try
        {
            MMI.HideGraphicRoute(_routeTrack);
        }
        catch
        {
        }
        _routeTrack = null;
    }

    void ReloadAfterAction(FDP2.FDR fdr)
    {
        if (_fdr == fdr && _mode == Mode.View)
        {
            LoadFieldData(true);
            StampUpdateTime();
            RefreshState();
        }
    }

    // =====================================================================
    //  Messages
    // =====================================================================

    /// <summary>Runs a UI action; any failure is shown in the message area instead of crashing vatSys.</summary>
    void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (FDRParseException ex)
        {
            ShowError(ex);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            _errorReporter.ReportError(ex, "An error occurred in the Flight Plan Window");
            try
            {
                RefreshState();
            }
            catch
            {
            }
        }
    }

    void ShowError(FDRParseException ex)
    {
        ShowError(string.IsNullOrEmpty(ex.OffendingElement) ? ex.Cause : "'" + ex.OffendingElement + "' : " + ex.Cause);
    }

    void ShowError(string message)
    {
        Color warning = EcTheme.Warning;
        _errorPanel.BackColor = warning;
        _errorLabel.BackColor = warning;
        _errorLabel.Text = message;
        _errorLabel.Visible = true;
        _acidField.ErrorMode(true);
        FlushPaint();
    }

    void ClearError()
    {
        if (!_errorLabel.Visible)
        {
            return;
        }
        _errorLabel.Visible = false;
        _errorLabel.Text = "";
        _errorLabel.BackColor = EcTheme.Background;
        _errorPanel.BackColor = EcTheme.Background;
        _acidField.ErrorMode(false);
        FlushPaint();
    }

    // =====================================================================
    //  Teardown
    // =====================================================================

    protected override void OnClosing(CancelEventArgs e)
    {
        Unsubscribe();
        CancelPendingRequest();
        HideGraphicRoute();
        FDP2.FDRsChanged -= FDP2_FDRsChanged;
        Network.ValidATCChanged -= Network_ValidATCChanged;
        MMI.SelectedTrackChanged -= MMI_SelectedTrackChanged;
        base.OnClosing(e);
    }
}
