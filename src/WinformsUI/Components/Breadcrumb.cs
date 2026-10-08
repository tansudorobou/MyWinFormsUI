using System.ComponentModel;

namespace WinformsUI;

public sealed record BreadcrumbEntry(string Key, string Text, Action? Navigate = null);
public sealed class BreadcrumbNavigationEventArgs(int index, string key) : EventArgs
{
    public int Index { get; } = index;
    public string Key { get; } = key;
}

public sealed class Breadcrumb : UserControl
{
    private readonly FlowLayoutPanel _row = WinformsUI.Layout.Wrap();
    private readonly ContextMenuStrip _overflow = new();
    private BreadcrumbEntry[] _path = [];
    private int _maxVisible = 5;
    public event EventHandler<BreadcrumbNavigationEventArgs>? NavigationRequested;
    public Breadcrumb(params BreadcrumbEntry[] path) { _row.Dock = DockStyle.Fill; Controls.Add(_row); SetPath(path); }
    public IReadOnlyList<BreadcrumbEntry> Path => Array.AsReadOnly(_path);
    [DefaultValue(5)]
    public int MaxVisible
    {
        get => _maxVisible;
        set { ArgumentOutOfRangeException.ThrowIfLessThan(value, 2); _maxVisible = value; Rebuild(); }
    }
    public void SetPath(params BreadcrumbEntry[] path) { _path = path.ToArray(); Rebuild(); }
    private void Navigate(int index)
    {
        _path[index].Navigate?.Invoke();
        NavigationRequested?.Invoke(this, new BreadcrumbNavigationEventArgs(index, _path[index].Key));
    }
    private void Rebuild()
    {
        foreach (Control control in _row.Controls.Cast<Control>().ToArray()) control.Dispose();
        _overflow.Items.Clear();
        for (int i = 0; i < _path.Length; i++)
        {
            int index = i;
            bool hidden = _path.Length > _maxVisible && i > 0 && i < _path.Length - (_maxVisible - 1);
            if (hidden)
            {
                _overflow.Items.Add(_path[i].Text, null, (_, _) => Navigate(index));
                if (i == 1)
                {
                    var ellipsis = new LinkLabel { Text = "…", AutoSize = true, AccessibleName = "省略された階層を表示" };
                    ellipsis.LinkClicked += (_, _) => _overflow.Show(ellipsis, new Point(0, ellipsis.Height));
                    _row.Controls.Add(ellipsis);
                    _row.Controls.Add(new Label { Text = "/", AutoSize = true });
                }
                continue;
            }
            var link = new LinkLabel { Text = _path[i].Text, AutoSize = true, Enabled = i < _path.Length - 1 };
            link.LinkClicked += (_, _) => Navigate(index);
            _row.Controls.Add(link);
            if (i < _path.Length - 1) _row.Controls.Add(new Label { Text = "/", AutoSize = true });
        }
        LayoutInvalidation.NotifyAncestors(this);
    }
    public override Size GetPreferredSize(Size proposedSize) => _row is null ? base.GetPreferredSize(proposedSize) : _row.GetPreferredSize(proposedSize);
    protected override void Dispose(bool disposing) { if (disposing) _overflow.Dispose(); base.Dispose(disposing); }
}
