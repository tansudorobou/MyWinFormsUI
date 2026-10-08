using System.ComponentModel;

namespace WinformsUI;

/// <summary>A root Stack that manages its owning Form's initial/preset client size.</summary>
public sealed class RootPanel : StackPanel
{
    private readonly Form _form;
    private RootSize _preset;
    private bool _fitToWorkingArea = true;

    internal RootPanel(Form form, RootSize preset)
    {
        _ = RootSizes.GetSize(preset);
        _form = form;
        _preset = preset;
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

    /// <summary>Reduce oversized presets to the monitor working area while preserving 16:9.</summary>
    [DefaultValue(true)]
    public bool FitToWorkingArea
    {
        get => _fitToWorkingArea;
        set { _fitToWorkingArea = value; ApplySize(); }
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
        Size desired = RootSizes.GetSize(_preset);
        if (_fitToWorkingArea)
        {
            Size working = System.Windows.Forms.Screen.FromControl(_form).WorkingArea.Size;
            Size frame = _form.Size - _form.ClientSize;
            desired = RootSizes.Fit(_preset, new Size(Math.Max(16, working.Width - frame.Width), Math.Max(9, working.Height - frame.Height)));
        }
        _form.ClientSize = desired;
    }

    private void FormShown(object? sender, EventArgs e) => ApplySize();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _form.Shown -= FormShown;
        base.Dispose(disposing);
    }
}
