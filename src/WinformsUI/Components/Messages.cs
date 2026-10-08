using System.ComponentModel;

namespace WinformsUI;

public sealed class ReactionChangedEventArgs(string reaction, bool selected, int count) : EventArgs
{
    public string Reaction { get; } = reaction;
    public bool Selected { get; } = selected;
    public int Count { get; } = count;
}

public sealed class MessageBubble : UserControl
{
    private readonly TextBox _text = new() { Multiline = true, ReadOnly = true, WordWrap = true, BorderStyle = BorderStyle.FixedSingle };
    private readonly FlowLayoutPanel _reactions = WinformsUI.Layout.Wrap();
    private readonly StackPanel _extras = WinformsUI.Layout.Stack();
    private readonly Button _expand;
    private bool _expanded;
    private int _maximumLines = 6;
    private bool _arranging;
    public event EventHandler<ReactionChangedEventArgs>? ReactionChanged;
    public MessageBubble(string text)
    {
        _text.Text = text; _expand = Ui.Button("もっと見る", () => { Expanded = !Expanded; });
        _extras.Visible = false; _reactions.Visible = false;
        Controls.AddRange([_text, _expand, _extras, _reactions]); Margin = Padding.Empty; TabStop = false;
    }
    public string BodyText => _text.Text;
    public void SetText(string text) { _text.Text = text; PerformLayout(); LayoutInvalidation.NotifyAncestors(this); }
    [DefaultValue(6)]
    public int MaximumLines
    {
        get => _maximumLines;
        set { ArgumentOutOfRangeException.ThrowIfLessThan(value, 1); _maximumLines = value; PerformLayout(); LayoutInvalidation.NotifyAncestors(this); }
    }
    [DefaultValue(false)]
    public bool Expanded
    {
        get => _expanded;
        set { _expanded = value; _expand.Text = value ? "折りたたむ" : "もっと見る"; PerformLayout(); LayoutInvalidation.NotifyAncestors(this); }
    }
    public CheckBox AddReaction(string text, int count = 0, bool selected = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        int others = Math.Max(0, count - (selected ? 1 : 0));
        var button = new CheckBox { Appearance = Appearance.Button, AutoSize = true, Checked = selected, Text = $"{text} {count}", AccessibleName = text };
        button.CheckedChanged += (_, _) =>
        {
            int total = others + (button.Checked ? 1 : 0); button.Text = $"{text} {total}";
            ReactionChanged?.Invoke(this, new ReactionChangedEventArgs(text, button.Checked, total));
            LayoutInvalidation.NotifyAncestors(this);
        };
        _reactions.Controls.Add(button); _reactions.Visible = true; LayoutInvalidation.NotifyAncestors(this); return button;
    }
    public void AddContent(params Control[] controls) { _extras.Add(controls); _extras.Visible = _extras.Controls.Count > 0; LayoutInvalidation.NotifyAncestors(this); }
    private int TextHeight(int width) => TextRenderer.MeasureText(_text.Text.Length == 0 ? " " : _text.Text, _text.Font,
        new Size(Math.Max(1, width - 8), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding).Height + 8;
    public override Size GetPreferredSize(Size proposedSize)
    {
        if (_text is null) return base.GetPreferredSize(proposedSize);
        int width = proposedSize.Width > 0 ? proposedSize.Width : Math.Max(Width, 240);
        int full = TextHeight(width), limit = _text.Font.Height * _maximumLines + 8;
        int height = _expanded ? full : Math.Min(full, limit);
        if (full > limit) height += _expand.GetPreferredSize(new Size(width, 0)).Height + 4;
        if (_extras.Controls.Count > 0) height += _extras.GetPreferredSize(new Size(width, 0)).Height + 4;
        if (_reactions.Controls.Count > 0) height += _reactions.GetPreferredSize(new Size(width, 0)).Height + 4;
        return new Size(width, height);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e); if (_text is null || _arranging) return; _arranging = true;
        try
        {
            int full = TextHeight(Width), limit = _text.Font.Height * _maximumLines + 8;
            int y = _expanded ? full : Math.Min(full, limit); _text.SetBounds(0, 0, Width, y);
            _expand.Visible = full > limit;
            if (_expand.Visible) { y += 4; int h = _expand.GetPreferredSize(new Size(Width, 0)).Height; _expand.SetBounds(0, y, Math.Min(Width, _expand.PreferredSize.Width), h); y += h; }
            if (_extras.Controls.Count > 0) { y += 4; int h = _extras.GetPreferredSize(new Size(Width, 0)).Height; _extras.SetBounds(0, y, Width, h); y += h; }
            if (_reactions.Controls.Count > 0) { y += 4; _reactions.SetBounds(0, y, Width, _reactions.GetPreferredSize(new Size(Width, 0)).Height); }
        }
        finally { _arranging = false; }
    }
}

public enum MessageAlignment { Start, End }

public sealed class ConversationMessage : UserControl
{
    private readonly Label _header;
    private readonly Control _footer;
    private readonly Avatar _avatar;
    private readonly MessageAlignment _alignment;
    public string Id { get; }
    public bool IsUser => _alignment == MessageAlignment.End;
    public MessageBubble Bubble { get; }
    public ConversationMessage(string id, string author, string text, MessageAlignment alignment = MessageAlignment.Start, Control? footer = null, Image? avatar = null)
    {
        Id = id; _alignment = alignment;
        _header = Ui.Label(author); _footer = footer ?? Ui.Label("");
        _avatar = new Avatar(author.Length > 0 ? author[..1] : "?", avatar) { Size = new Size(32, 32) };
        Bubble = new MessageBubble(text); Controls.AddRange([_avatar, _header, Bubble, _footer]); Margin = Padding.Empty; TabStop = false;
        AccessibleName = $"{author} のメッセージ";
    }
    private int ContentWidth(int width) => Math.Max(1, Math.Min(width - 44, (int)(width * .8)));
    public override Size GetPreferredSize(Size proposedSize)
    {
        if (Bubble is null) return base.GetPreferredSize(proposedSize);
        int width = proposedSize.Width > 0 ? proposedSize.Width : Math.Max(Width, 320);
        int contentWidth = ContentWidth(width);
        return new Size(width, _header.GetPreferredSize(new Size(contentWidth, 0)).Height
            + Bubble.GetPreferredSize(new Size(contentWidth, 0)).Height + _footer.GetPreferredSize(new Size(contentWidth, 0)).Height + 8);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e); if (Bubble is null) return;
        bool end = _alignment == MessageAlignment.End;
        if (RightToLeft == RightToLeft.Yes) end = !end;
        int width = ContentWidth(Width), x = end ? Math.Max(0, Width - width - 40) : 40;
        int headerHeight = _header.GetPreferredSize(new Size(width, 0)).Height;
        _header.SetBounds(x, 0, width, headerHeight);
        int bubbleHeight = Bubble.GetPreferredSize(new Size(width, 0)).Height;
        Bubble.SetBounds(x, headerHeight + 4, width, bubbleHeight);
        _footer.SetBounds(x, Bubble.Bottom + 4, width, _footer.GetPreferredSize(new Size(width, 0)).Height);
        _avatar.SetBounds(end ? Math.Max(0, Width - 32) : 0, Math.Max(0, Bubble.Bottom - 32), 32, 32);
    }
}
