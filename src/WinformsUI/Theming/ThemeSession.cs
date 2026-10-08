using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinformsUI;

public static class UiTheme
{
    private sealed class RoleInfo { public string? Role; }
    private static readonly ConditionalWeakTable<Control, RoleInfo> Roles = new();
    private static readonly ConditionalWeakTable<Control, ThemeSession> Sessions = new();

    public static T WithRole<T>(this T control, string role) where T : Control
    {
        Roles.GetOrCreateValue(control).Role = role;
        FindSession(control)?.RefreshControl(control);
        return control;
    }

    internal static string RoleFor(Control control) => Roles.TryGetValue(control, out var info) && info.Role is not null
        ? info.Role : control switch
        {
            ButtonBase => "button", TextBoxBase or ComboBox or UpDownBase or DateTimePicker => "input",
            DataGridView => "table", Label => "label", _ => "surface"
        };

    public static ThemeSession Attach(Control root, ThemeTemplate? template = null)
    {
        if (Sessions.TryGetValue(root, out _)) throw new InvalidOperationException("このルートにはすでにテーマが適用されています。");
        var session = new ThemeSession(root, template ?? ThemeTemplate.Native);
        Sessions.Add(root, session); return session;
    }
    internal static void Detach(Control root) => Sessions.Remove(root);
    internal static void Refresh(Control control) => FindSession(control)?.RefreshControl(control);
    private static ThemeSession? FindSession(Control control)
    {
        for (Control? current = control; current is not null; current = current.Parent)
            if (Sessions.TryGetValue(current, out var session)) return session;
        return null;
    }
    public static ThemeSession? Inherit(Control child, Control owner)
    {
        var parent = FindSession(owner); if (parent is null) return null;
        var session = Attach(child, parent.Template);
        void Changed(object? sender, EventArgs e) => session.Use(parent.Template);
        parent.TemplateChanged += Changed;
        session.Disposed += (_, _) => parent.TemplateChanged -= Changed;
        return session;
    }
}

/// <summary>Applies a template to an entire control tree, including controls added later.</summary>
public sealed class ThemeSession : IDisposable
{
    private readonly Control _root;
    private readonly Dictionary<Control, Baseline> _baselines = [];
    private readonly Dictionary<(string Family, float Size, FontStyle Style), Font> _themeFonts = [];
    private readonly Font _nativeFont;
    private readonly Color _nativeBackground;
    private readonly Color _nativeForeground;
    private bool _disposed;
    private ThemeTemplate _template = ThemeTemplate.Native;

    internal ThemeSession(Control root, ThemeTemplate template)
    {
        ArgumentNullException.ThrowIfNull(root);
        _root = root;
        _nativeFont = new Font(root.Font, root.Font.Style);
        _nativeBackground = root.BackColor; _nativeForeground = root.ForeColor;
        CaptureTree(root);
        _root.Disposed += RootDisposed;
        Use(template);
    }

    public ThemeTemplate Template => _template;
    public event EventHandler? TemplateChanged;
    public event EventHandler? Disposed;

    public void Use(ThemeTemplate template)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(template);
        template.Validate();
        _root.SuspendLayout();
        try
        {
            foreach (var (control, baseline) in _baselines.ToArray()) if (!control.IsDisposed) baseline.Restore(control);
            ReleaseThemeFonts();
            _template = template;
            foreach (var control in _baselines.Keys.ToArray()) if (!control.IsDisposed) Apply(control);
        }
        finally { _root.ResumeLayout(true); }
        TemplateChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void RefreshControl(Control control)
    {
        if (_disposed || !_baselines.TryGetValue(control, out var baseline)) return;
        baseline.Restore(control); Apply(control);
    }

    private void CaptureTree(Control root)
    {
        if (_baselines.ContainsKey(root)) return;
        var descriptors = TypeDescriptor.GetProperties(root);
        Font nativeFont = descriptors[nameof(Control.Font)]?.ShouldSerializeValue(root) == true ? root.Font : _nativeFont;
        bool themed = _template.Background is not null || _template.Foreground is not null || _template.FontSize is not null || _template.FontFamily is not null || _template.Roles.Count > 0;
        Color background = root.BackColor, foreground = root.ForeColor;
        if (themed && descriptors[nameof(Control.BackColor)]?.ShouldSerializeValue(root) != true)
            background = root is TextBoxBase or ComboBox or ListView or ListBox or UpDownBase ? SystemColors.Window : _nativeBackground;
        if (themed && descriptors[nameof(Control.ForeColor)]?.ShouldSerializeValue(root) != true) foreground = _nativeForeground;
        Baseline baseline;
        if (themed && root is Label && root.Parent is IInputField field && root.AccessibleName == field.LabelText)
        {
            using var labelFont = new Font(_nativeFont.FontFamily, Math.Max(8, _nativeFont.SizeInPoints - 1));
            baseline = new Baseline(root, labelFont, background, foreground);
        }
        else baseline = new Baseline(root, nativeFont, background, foreground);
        _baselines.Add(root, baseline);
        root.ControlAdded += ChildAdded;
        root.Disposed += ChildDisposed;
        foreach (Control child in root.Controls) CaptureTree(child);
    }

    private void ChildAdded(object? sender, ControlEventArgs e)
    {
        if (_disposed || e.Control is null) return;
        CaptureTree(e.Control);
        ApplyTree(e.Control);
    }

    private void ApplyTree(Control root)
    {
        Apply(root);
        foreach (Control child in root.Controls) ApplyTree(child);
    }

    private void Apply(Control control)
    {
        var baseline = _baselines[control];
        _template.Roles.TryGetValue(UiTheme.RoleFor(control), out var style);
        control.BackColor = ThemeTemplate.ParseColor(style?.Background ?? _template.Background) ?? baseline.Background;
        control.ForeColor = ThemeTemplate.ParseColor(style?.Foreground ?? _template.Foreground) ?? baseline.Foreground;
        if (style?.FontFamily is not null || style?.FontSize is not null || _template.FontFamily is not null || _template.FontSize is not null)
        {
            var key = (style?.FontFamily ?? _template.FontFamily ?? baseline.Font.FontFamily.Name, style?.FontSize ?? _template.FontSize ?? baseline.Font.SizeInPoints, baseline.Font.Style);
            if (!_themeFonts.TryGetValue(key, out var font)) { font = new Font(key.Item1, key.Item2, key.Item3); _themeFonts.Add(key, font); }
            control.Font = font;
        }
        if (control is ContentPanel panel && _template.Gap is int gap) panel.Gap = gap;
        if (control is RootPanel && _template.RootPadding is int padding)
            control.Padding = new Padding((int)Math.Round(padding * control.DeviceDpi / 96d));
        if (control is Button button && (style?.Background ?? _template.Background) is not null) button.UseVisualStyleBackColor = false;
        if (control is DataGridView grid && (style?.Background is not null || style?.Foreground is not null || _template.Background is not null || _template.Foreground is not null))
        {
            grid.EnableHeadersVisualStyles = _template == ThemeTemplate.Native;
            grid.BackgroundColor = control.BackColor;
            grid.DefaultCellStyle.BackColor = control.BackColor;
            grid.DefaultCellStyle.ForeColor = control.ForeColor;
            grid.ColumnHeadersDefaultCellStyle.BackColor = control.BackColor;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = control.ForeColor;
        }
        if (control is IThemeAware aware) aware.ApplyTheme(_template);
        RoundedCorners.Apply(control, style?.CornerRadius ?? (control is Button or TextBoxBase or ComboBox or UpDownBase ? _template.CornerRadius : null));
        control.Invalidate();
    }

    private void ChildDisposed(object? sender, EventArgs e)
    {
        if (sender is not Control control) return;
        control.ControlAdded -= ChildAdded;
        control.Disposed -= ChildDisposed;
        _baselines.Remove(control);
    }

    private void RootDisposed(object? sender, EventArgs e) => Dispose();
    private void ReleaseThemeFonts() { foreach (var font in _themeFonts.Values) font.Dispose(); _themeFonts.Clear(); }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; UiTheme.Detach(_root);
        _root.Disposed -= RootDisposed;
        foreach (var (control, baseline) in _baselines.ToArray())
        {
            control.ControlAdded -= ChildAdded;
            control.Disposed -= ChildDisposed;
            if (!control.IsDisposed) baseline.Restore(control);
        }
        _baselines.Clear();
        ReleaseThemeFonts();
        _nativeFont.Dispose();
        Disposed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Baseline
    {
        internal Color Background { get; }
        internal Color Foreground { get; }
        internal Font Font { get; }
        private readonly Padding _padding;
        private readonly int? _gap;
        private readonly bool _visualButton;
        private readonly DataGridViewCellStyle? _cellStyle;
        private readonly DataGridViewCellStyle? _headerStyle;
        private readonly Color _gridBackground;
        private readonly bool _headerVisual;

        internal Baseline(Control control, Font font, Color background, Color foreground)
        {
            Background = background; Foreground = foreground;
            Font = new Font(font, font.Style);
            // The restored font stays valid even if the session is disposed before the control.
            Font retainedFont = Font;
            control.Disposed += (_, _) => retainedFont.Dispose();
            _padding = control.Padding;
            _gap = (control as ContentPanel)?.Gap;
            _visualButton = (control as Button)?.UseVisualStyleBackColor ?? false;
            if (control is DataGridView grid)
            {
                _cellStyle = grid.DefaultCellStyle.Clone(); _headerStyle = grid.ColumnHeadersDefaultCellStyle.Clone();
                _gridBackground = grid.BackgroundColor; _headerVisual = grid.EnableHeadersVisualStyles;
            }
        }

        internal void Restore(Control control)
        {
            control.BackColor = Background; control.ForeColor = Foreground; control.Font = Font; control.Padding = _padding;
            if (control is ContentPanel panel && _gap is int gap) panel.Gap = gap;
            if (control is Button button) button.UseVisualStyleBackColor = _visualButton;
            if (control is DataGridView grid)
            {
                grid.DefaultCellStyle = _cellStyle!.Clone(); grid.ColumnHeadersDefaultCellStyle = _headerStyle!.Clone();
                grid.BackgroundColor = _gridBackground; grid.EnableHeadersVisualStyles = _headerVisual;
            }
            if (control is IThemeAware aware) aware.ApplyTheme(ThemeTemplate.Native);
            RoundedCorners.Apply(control, null);
        }
    }
}
