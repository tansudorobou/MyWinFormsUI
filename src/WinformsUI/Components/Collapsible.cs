using System.ComponentModel;

namespace WinformsUI;

public sealed class CollapsibleSection : UserControl
{
    private bool _expanded;
    public Button Trigger { get; }
    public Control Content { get; }
    public event EventHandler? ExpandedChanged;

    public CollapsibleSection(string title, Control content, bool expanded = true)
    {
        Content = content;
        _expanded = expanded;
        Trigger = Ui.Button(title, () => Expanded = !Expanded);
        Trigger.AccessibleDescription = "内容を展開または折りたたみます。";
        Controls.AddRange([Trigger, content]);
        content.Visible = expanded;
        Margin = Padding.Empty;
        TabStop = false;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Expanded
    {
        get => _expanded;
        set
        {
            if (_expanded == value) return;
            _expanded = value; Content.Visible = value;
            Trigger.AccessibleDescription = value ? "展開済み。押すと折りたたみます。" : "折りたたみ済み。押すと展開します。";
            PerformLayout(); LayoutInvalidation.NotifyAncestors(this);
            ExpandedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (Trigger is null) return base.GetPreferredSize(proposedSize);
        int width = proposedSize.Width > 0 ? proposedSize.Width : Math.Max(Width, 240);
        return new Size(width, Trigger.GetPreferredSize(new Size(width, 0)).Height
            + (_expanded ? Content.GetPreferredSize(new Size(width, 0)).Height + 8 : 0));
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (Trigger is null) return;
        int height = Trigger.GetPreferredSize(new Size(Width, 0)).Height;
        Trigger.SetBounds(0, 0, Width, height);
        Content.SetBounds(0, height + 8, Width, Math.Max(0, Height - height - 8));
    }
}

public sealed class AccordionPanel : UserControl
{
    private readonly StackPanel _stack = WinformsUI.Layout.Stack();
    public AccordionPanel(bool allowMultiple = false)
    {
        AllowMultiple = allowMultiple;
        _stack.Dock = DockStyle.Fill; Controls.Add(_stack); Margin = Padding.Empty;
    }
    [DefaultValue(false)]
    public bool AllowMultiple { get; set; }
    public IReadOnlyList<CollapsibleSection> Sections => _stack.Controls.OfType<CollapsibleSection>().ToArray();

    public CollapsibleSection AddSection(string title, Control content, bool expanded = false)
    {
        var section = new CollapsibleSection(title, content, expanded);
        section.ExpandedChanged += (_, _) =>
        {
            if (AllowMultiple || !section.Expanded) return;
            foreach (var other in Sections) if (other != section) other.Expanded = false;
        };
        if (expanded && !AllowMultiple) foreach (var other in Sections) other.Expanded = false;
        _stack.Add(section);
        LayoutInvalidation.NotifyAncestors(this);
        return section;
    }
    public override Size GetPreferredSize(Size proposedSize) => _stack is null ? base.GetPreferredSize(proposedSize) : _stack.GetPreferredSize(proposedSize);
}
