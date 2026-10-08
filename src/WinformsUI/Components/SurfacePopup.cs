using System.ComponentModel;

namespace WinformsUI;

/// <summary>Reusable rich popover/hover preview over a native ToolStripDropDown.</summary>
public sealed class SurfacePopup : IDisposable
{
    private readonly ToolStripDropDown _popup = new() { Padding = Padding.Empty, AutoClose = true };
    private readonly System.Windows.Forms.Timer _openTimer = new();
    private readonly System.Windows.Forms.Timer _closeTimer = new();
    private Control? _trigger;
    private bool _disposed;
    private ThemeSession? _theme;
    private bool _hoverAttached;
    public bool IsOpen => _popup.Visible;
    public Control Content { get; }
    public SurfacePopup(Control content, Size size)
    {
        Content = content; content.Size = size;
        var host = new ToolStripControlHost(content) { AutoSize = false, Size = size, Margin = Padding.Empty, Padding = Padding.Empty };
        _popup.Items.Add(host);
        _openTimer.Tick += (_, _) => { _openTimer.Stop(); if (_trigger is not null && !_trigger.IsDisposed) Show(_trigger); };
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            bool overTrigger = _trigger is not null && !_trigger.IsDisposed && _trigger.RectangleToScreen(_trigger.ClientRectangle).Contains(Cursor.Position);
            if (overTrigger || _popup.Bounds.Contains(Cursor.Position) || Content.ContainsFocus || _trigger?.ContainsFocus == true) return;
            Close();
        };
        _popup.MouseEnter += (_, _) => _closeTimer.Stop();
        _popup.MouseLeave += (_, _) => _closeTimer.Start();
        content.MouseEnter += (_, _) => _closeTimer.Stop();
        content.MouseLeave += (_, _) => _closeTimer.Start();
        _closeTimer.Interval = 200;
    }
    public void Show(Control trigger)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _trigger = trigger;
        _theme ??= UiTheme.Inherit(Content, trigger);
        _popup.Show(trigger, new Point(0, trigger.Height));
        if (!_hoverAttached) Content.Focus();
    }
    public void Close() { _openTimer.Stop(); _closeTimer.Stop(); _popup.Close(); }
    public void AttachHover(Control trigger, int openDelay = 400, int closeDelay = 200)
    {
        if (_trigger is not null) throw new InvalidOperationException("トリガーはすでに設定されています。");
        ArgumentOutOfRangeException.ThrowIfLessThan(openDelay, 1); ArgumentOutOfRangeException.ThrowIfLessThan(closeDelay, 1);
        _trigger = trigger; _hoverAttached = true; _openTimer.Interval = openDelay; _closeTimer.Interval = closeDelay;
        trigger.MouseEnter += TriggerEntered; trigger.MouseLeave += TriggerLeft;
        trigger.GotFocus += TriggerEntered; trigger.LostFocus += TriggerLeft;
        trigger.Disposed += TriggerDisposed;
    }
    private void TriggerEntered(object? sender, EventArgs e) { _closeTimer.Stop(); _openTimer.Start(); }
    private void TriggerLeft(object? sender, EventArgs e) { _openTimer.Stop(); _closeTimer.Start(); }
    private void TriggerDisposed(object? sender, EventArgs e) => Dispose();
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        if (_trigger is not null)
        {
            _trigger.MouseEnter -= TriggerEntered; _trigger.MouseLeave -= TriggerLeft;
            _trigger.GotFocus -= TriggerEntered; _trigger.LostFocus -= TriggerLeft; _trigger.Disposed -= TriggerDisposed;
        }
        _theme?.Dispose(); _openTimer.Dispose(); _closeTimer.Dispose(); _popup.Dispose();
    }
}

public enum SheetSide { Left, Right, Top, Bottom }

/// <summary>Side-positioned content using native Form modality, focus trapping and close semantics.</summary>
public class SheetForm : Form
{
    private readonly Form _owner;
    private readonly SheetSide _side;
    private int _extent;
    private ThemeSession? _theme;
    public SheetForm(Form owner, Control content, SheetSide side = SheetSide.Right, int extent = 360)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(extent, 1);
        _owner = owner; _side = side; _extent = extent;
        ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; FormBorderStyle = FormBorderStyle.SizableToolWindow;
        Text = "詳細"; content.Dock = DockStyle.Fill; Controls.Add(content); KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        Shown += (_, _) => { _theme ??= UiTheme.Inherit(this, _owner); PositionSheet(); };
        _owner.LocationChanged += OwnerMoved; _owner.Resize += OwnerMoved;
    }
    public void SnapTo(int extent) { ArgumentOutOfRangeException.ThrowIfLessThan(extent, 1); _extent = extent; PositionSheet(); }
    protected int Extent => _extent;
    private void OwnerMoved(object? sender, EventArgs e) { if (Visible) PositionSheet(); }
    private void PositionSheet()
    {
        Rectangle area = _owner.RectangleToScreen(_owner.ClientRectangle);
        Rectangle target = _side switch
        {
            SheetSide.Left => new Rectangle(area.Left, area.Top, Math.Min(_extent, area.Width), area.Height),
            SheetSide.Right => new Rectangle(Math.Max(area.Left, area.Right - _extent), area.Top, Math.Min(_extent, area.Width), area.Height),
            SheetSide.Top => new Rectangle(area.Left, area.Top, area.Width, Math.Min(_extent, area.Height)),
            _ => new Rectangle(area.Left, Math.Max(area.Top, area.Bottom - _extent), area.Width, Math.Min(_extent, area.Height))
        };
        Bounds = target;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _theme?.Dispose(); _owner.LocationChanged -= OwnerMoved; _owner.Resize -= OwnerMoved; }
        base.Dispose(disposing);
    }
}

/// <summary>Sheet plus a pointer drag handle and optional snap sizes. No animation dependency.</summary>
public sealed class DrawerForm : SheetForm
{
    private readonly SheetSide _side;
    private Point? _dragStart;
    private int[] _snapPoints = [];
    public Panel DragHandle { get; } = new() { Height = 18, Dock = DockStyle.Top, BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.SizeAll, AccessibleName = "ドラッグしてサイズ変更または閉じる" };
    public DrawerForm(Form owner, Control content, SheetSide side = SheetSide.Bottom, int extent = 320) : base(owner, content, side, extent)
    {
        _side = side; Controls.Add(DragHandle); DragHandle.BringToFront();
        DragHandle.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { _dragStart = DragHandle.PointToScreen(e.Location); DragHandle.Capture = true; } };
        DragHandle.MouseUp += (_, e) =>
        {
            if (_dragStart is not { } start) return;
            Point end = DragHandle.PointToScreen(e.Location); _dragStart = null; DragHandle.Capture = false;
            int distance = _side is SheetSide.Left or SheetSide.Right ? end.X - start.X : end.Y - start.Y;
            if (_side is SheetSide.Left or SheetSide.Top) distance = -distance;
            if (distance > Extent / 2) { Close(); return; }
            int target = Math.Max(1, Extent - distance);
            SnapTo(_snapPoints.Length == 0 ? target : _snapPoints.MinBy(size => Math.Abs(size - target)));
        };
    }
    public void SetSnapPoints(params int[] sizes)
    {
        if (sizes.Any(size => size < 1)) throw new ArgumentOutOfRangeException(nameof(sizes));
        _snapPoints = sizes.Distinct().Order().ToArray();
    }
}
