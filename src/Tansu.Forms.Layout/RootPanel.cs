using System.ComponentModel;

namespace Tansu.Forms.Layout;

/// <summary>A root Stack that manages its owning Form's initial/preset client size.</summary>
public sealed class RootPanel : StackPanel
{
    private readonly Form _form;
    private RootSize _preset;
    private bool _fitToWorkingArea = true;
    private bool _autoMinimumSize;
    private Size _minimumClientSize;
    private readonly Size _originalMinimumSize;
    private Size? _lastMinimumSize;
    private int _minimumDpi;

    internal RootPanel(Form form, RootSize preset)
    {
        _ = RootSizes.GetSize(preset);
        _form = form;
        _preset = preset;
        _originalMinimumSize = form.MinimumSize;
        _autoMinimumSize = _originalMinimumSize.IsEmpty;
        Dock = DockStyle.Fill;
        AutoScroll = true;
        Padding = new Padding(16);
        // WinForms may scale a pre-show ClientSize. Apply pixel-based presets again after that scaling.
        _form.Shown += FormShown;
    }

    [DefaultValue(RootSize.Hd)]
    public RootSize Preset
    {
        get => _preset;
        set { _ = RootSizes.GetSize(value); _preset = value; ApplySize(); }
    }

    /// <summary>Reduce oversized presets to the monitor working area while preserving their aspect ratio.</summary>
    [DefaultValue(true)]
    public bool FitToWorkingArea
    {
        get => _fitToWorkingArea;
        set { _fitToWorkingArea = value; ApplySize(); }
    }

    /// <summary>Automatically set the owning Form's minimum size. Existing explicit minimums are preserved.</summary>
    [DefaultValue(true)]
    public bool AutoMinimumSize
    {
        get => _autoMinimumSize;
        set
        {
            CanManageMinimumSize();
            if (_autoMinimumSize == value) return;
            _autoMinimumSize = value;
            if (value) { _lastMinimumSize = null; ApplyMinimumSize(FittedSize()); }
            else if (_lastMinimumSize == _form.MinimumSize) _form.MinimumSize = _originalMinimumSize;
        }
    }

    /// <summary>Override the automatic minimum content size in logical pixels. Empty uses the preset recommendation.</summary>
    [DefaultValue(typeof(Size), "0, 0")]
    public Size MinimumClientSize
    {
        get => _minimumClientSize;
        set
        {
            if (!value.IsEmpty && (value.Width <= 0 || value.Height <= 0)) throw new ArgumentOutOfRangeException(nameof(value));
            _minimumClientSize = value;
            if (_autoMinimumSize) ApplyMinimumSize(FittedSize());
        }
    }

    public RootPanel UseSize(RootSize preset)
    {
        _ = RootSizes.GetSize(preset);
        if (_form.WindowState != FormWindowState.Normal) _form.WindowState = FormWindowState.Normal;
        Preset = preset;
        return this;
    }

    internal void ApplySize()
    {
        if (_form.IsDisposed || _form.WindowState != FormWindowState.Normal) return;
        Size desired = FittedSize();
        // An earlier automatic minimum must not prevent switching to a smaller/portrait preset.
        if (CanManageMinimumSize()) { _form.MinimumSize = Size.Empty; _lastMinimumSize = Size.Empty; _minimumDpi = _form.DeviceDpi; }
        _form.ClientSize = desired;
        ApplyMinimumSize(desired);
    }

    private Size FittedSize()
    {
        var definition = RootSizes.GetPreset(_preset);
        Size desired = definition.Size;
        if (_fitToWorkingArea)
        {
            Size working = System.Windows.Forms.Screen.FromControl(_form).WorkingArea.Size;
            Size frame = _form.Size - _form.ClientSize;
            desired = RootSizes.Fit(_preset, new Size(Math.Max(definition.RatioWidth, working.Width - frame.Width), Math.Max(definition.RatioHeight, working.Height - frame.Height)));
        }
        return desired;
    }

    private void ApplyMinimumSize(Size limit)
    {
        if (_form.IsDisposed || !CanManageMinimumSize()) return;
        Size logical = _minimumClientSize.IsEmpty ? RootSizes.GetMinimumClientSize(_preset) : _minimumClientSize;
        double scale = _form.DeviceDpi / 96d;
        Size content = new((int)Math.Min(limit.Width, Math.Round(logical.Width * scale)),
            (int)Math.Min(limit.Height, Math.Round(logical.Height * scale)));
        Size frame = _form.Size - _form.ClientSize;
        _form.MinimumSize = content + frame;
        _lastMinimumSize = _form.MinimumSize;
        _minimumDpi = _form.DeviceDpi;
    }

    private bool CanManageMinimumSize()
    {
        if (!_autoMinimumSize) return false;
        if (_lastMinimumSize is { } last && _form.MinimumSize != last)
        {
            // WinForms can scale its minimum during handle creation or a monitor DPI change.
            double scale = _form.DeviceDpi / (double)_minimumDpi;
            Size scaled = new((int)Math.Round(last.Width * scale), (int)Math.Round(last.Height * scale));
            if (_form.DeviceDpi == _minimumDpi || Math.Abs(_form.MinimumSize.Width - scaled.Width) > 1 || Math.Abs(_form.MinimumSize.Height - scaled.Height) > 1)
                _autoMinimumSize = false;
        }
        return _autoMinimumSize;
    }

    private void FormShown(object? sender, EventArgs e) => ApplySize();

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyMinimumSize(FittedSize());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _form.Shown -= FormShown;
            if (!_form.IsDisposed && !_form.Disposing && _autoMinimumSize && _lastMinimumSize == _form.MinimumSize)
                _form.MinimumSize = _originalMinimumSize;
        }
        base.Dispose(disposing);
    }
}
