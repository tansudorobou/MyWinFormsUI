using System.ComponentModel;

namespace WinformsUI;

/// <summary>A native editor with its label above it and optional help or error below it.</summary>
public sealed class InputField<T> : UserControl
{
    private readonly Label _label;
    private readonly Label _message;
    private readonly Func<T> _read;
    private readonly Action<T> _write;
    private readonly List<Func<T, string?>> _rules = [];
    private readonly bool _multiline;
    private string? _help;
    private Font? _labelFont;

    internal InputField(string label, Control editor, Func<T> read, Action<T> write, bool multiline = false)
    {
        _read = read;
        _write = write;
        _multiline = multiline;
        Editor = editor;
        Margin = Padding.Empty;
        TabStop = false;
        _label = new Label { Text = label, AutoSize = false, UseMnemonic = false, TabStop = false };
        _message = new Label { AutoSize = false, UseMnemonic = false, TabStop = false, Visible = false };
        editor.Margin = Padding.Empty;
        editor.TabIndex = 0;
        editor.AccessibleName = label;
        _label.AccessibleName = label;
        Controls.AddRange([_label, editor, _message]);
        UpdateLabelFont();
        Size = GetPreferredSize(new Size(220, 0));
    }

    public Control Editor { get; }
    public string LabelText => _label.Text;
    public string? ErrorMessage { get; private set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public T Value { get => _read(); set => _write(value); }
    public event EventHandler? ValueChanged;

    public InputField<T> Required(string? message = null) => ValidateWith(
        value => value is not null && (value is not string text || !string.IsNullOrWhiteSpace(text)),
        message ?? $"{LabelText}を入力してください。");

    public InputField<T> ValidateWith(Func<T, bool> predicate, string message)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _rules.Add(value => predicate(value) ? null : message);
        return this;
    }

    public InputField<T> WithHelp(string help)
    {
        _help = help;
        RefreshMessage();
        return this;
    }

    public bool ValidateValue()
    {
        ErrorMessage = _rules.Select(rule => rule(Value)).FirstOrDefault(error => error is not null);
        RefreshMessage();
        return ErrorMessage is null;
    }

    public void ClearValidation()
    {
        ErrorMessage = null;
        RefreshMessage();
    }

    internal void NotifyValueChanged()
    {
        if (ErrorMessage is not null) ClearValidation();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private int Scale(int logicalPixels) => (int)Math.Round(logicalPixels * DeviceDpi / 96d);

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (_label is null) return base.GetPreferredSize(proposedSize);
        int width = proposedSize.Width > 0 ? proposedSize.Width : Math.Max(Width, Scale(220));
        int innerWidth = Math.Max(1, width - Padding.Horizontal);
        int height = _label.GetPreferredSize(new Size(innerWidth, 0)).Height + Scale(4);
        height += _multiline ? Scale(72) : Editor.GetPreferredSize(new Size(innerWidth, 0)).Height;
        if (_message.Visible || !string.IsNullOrEmpty(_message.Text))
            height += Scale(4) + _message.GetPreferredSize(new Size(innerWidth, 0)).Height;
        return new Size(width, height + Padding.Vertical);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_label is null) return;
        int width = Math.Max(1, ClientSize.Width - Padding.Horizontal);
        int labelHeight = _label.GetPreferredSize(new Size(width, 0)).Height;
        _label.SetBounds(Padding.Left, Padding.Top, width, labelHeight);
        int editorHeight = _multiline ? Scale(72) : Editor.GetPreferredSize(new Size(width, 0)).Height;
        Editor.SetBounds(Padding.Left, _label.Bottom + Scale(4), width, editorHeight);
        _message.SetBounds(Padding.Left, Editor.Bottom + Scale(4), width,
            _message.GetPreferredSize(new Size(width, 0)).Height);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (_label is not null) UpdateLabelFont();
    }

    private void UpdateLabelFont()
    {
        var oldFont = _labelFont;
        _labelFont = new Font(Font.FontFamily, Math.Max(8, Font.SizeInPoints - 1), FontStyle.Regular);
        _label.Font = _labelFont;
        oldFont?.Dispose();
    }

    private void RefreshMessage()
    {
        _message.Text = ErrorMessage ?? _help ?? string.Empty;
        _message.ForeColor = ErrorMessage is null ? SystemColors.GrayText : Color.Firebrick;
        _message.Visible = _message.Text.Length > 0;
        PerformLayout();
        LayoutInvalidation.NotifyAncestors(this);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _labelFont?.Dispose();
    }
}
