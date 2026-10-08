namespace WinformsUI;

/// <summary>A stretchable native editor with prefix/suffix and block-level addons.</summary>
public sealed class InputGroup : UserControl
{
    private readonly TableLayoutPanel _layout = new() { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, RowCount = 3, Margin = Padding.Empty };
    public Control Editor { get; }
    public InputGroup(Control editor, Control? prefix = null, Control? suffix = null, Control? above = null, Control? below = null)
    {
        Editor = editor; Margin = Padding.Empty; TabStop = false;
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < 3; i++) _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        if (above is not null) { _layout.Controls.Add(above, 0, 0); _layout.SetColumnSpan(above, 3); }
        if (prefix is not null) _layout.Controls.Add(prefix, 0, 1);
        _layout.Controls.Add(editor, 1, 1); editor.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        if (suffix is not null) _layout.Controls.Add(suffix, 2, 1);
        if (below is not null) { _layout.Controls.Add(below, 0, 2); _layout.SetColumnSpan(below, 3); }
        Controls.Add(_layout);
    }
    public override Size GetPreferredSize(Size proposedSize) => _layout is null ? base.GetPreferredSize(proposedSize) : _layout.GetPreferredSize(proposedSize);
}
