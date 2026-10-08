using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;

namespace WinformsUI;

/// <summary>Optional rounded clipping for native controls; values are in logical pixels at 96 DPI.</summary>
public static class RoundedCorners
{
    private static readonly ConditionalWeakTable<Control, State> States = new();

    public static T WithRadius<T>(this T control, int radius) where T : Control
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        var state = States.GetValue(control, key => new State(key));
        state.Override = radius;
        state.Set(radius);
        return control;
    }

    public static void ResetRadius(this Control control)
    {
        if (States.TryGetValue(control, out var state)) { state.Override = null; state.Set(null); }
        UiTheme.Refresh(control);
    }

    internal static void Apply(Control control, int? radius)
    {
        if (States.TryGetValue(control, out var state)) state.Set(state.Override ?? radius);
        else if (radius is > 0) States.GetValue(control, key => new State(key)).Set(radius);
    }

    internal static GraphicsPath Path(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float diameter = Math.Min(Math.Max(0, radius * 2), Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0) { path.AddRectangle(bounds); return path; }
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure(); return path;
    }

    private sealed class State
    {
        private readonly Control _control;
        private readonly Region? _original;
        private int? _radius;
        internal int? Override;
        internal State(Control control)
        {
            _control = control; _original = control.Region?.Clone();
            control.SizeChanged += Changed; control.DpiChangedAfterParent += Changed;
            control.Disposed += (_, _) => _original?.Dispose();
        }
        internal void Set(int? radius) { _radius = radius; Update(); }
        private void Changed(object? sender, EventArgs e) => Update();
        private void Update()
        {
            if (_control.IsDisposed) return;
            if (_radius is not > 0 || _control.Width <= 0 || _control.Height <= 0)
                _control.Region = _original?.Clone();
            else
            {
                using var path = Path(new RectangleF(0, 0, _control.Width, _control.Height), _radius.Value * _control.DeviceDpi / 96f);
                _control.Region = new Region(path);
            }
            _control.Invalidate();
        }
    }
}
