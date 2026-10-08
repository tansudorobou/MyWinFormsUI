namespace WinformsUI;

public sealed record ChoiceItem<TKey>(TKey Key, string Text, string Group = "", bool Enabled = true) where TKey : notnull;

/// <summary>Searchable grouped single/multiple choices; native ComboBox remains the simple select.</summary>
public sealed class ChoicePicker<TKey> : UserControl where TKey : notnull
{
    private readonly List<ChoiceItem<TKey>> _choices;
    private readonly HashSet<TKey> _selected = [];
    private bool _updating;
    private readonly bool _multiple;
    public TextBox Query { get; } = new() { Dock = DockStyle.Top, PlaceholderText = "候補を検索" };
    public ListView Options { get; } = new() { Dock = DockStyle.Fill, View = View.Details, HeaderStyle = ColumnHeaderStyle.None, FullRowSelect = true, HideSelection = false };
    public event EventHandler? SelectionChanged;

    public ChoicePicker(IEnumerable<ChoiceItem<TKey>> choices, bool multiple = false)
    {
        _choices = choices.ToList(); _multiple = multiple;
        if (_choices.Select(choice => choice.Key).Distinct().Count() != _choices.Count) throw new ArgumentException("選択肢のキーが重複しています。", nameof(choices));
        Options.CheckBoxes = multiple; Options.MultiSelect = multiple; Options.Columns.Add("候補", 200);
        Controls.AddRange([Options, Query]); MinimumSize = new Size(0, 160); Height = 200;
        Query.TextChanged += (_, _) => Rebuild();
        Options.Resize += (_, _) => Options.Columns[0].Width = Math.Max(1, Options.ClientSize.Width - 24);
        Options.ItemCheck += (_, e) =>
        {
            if (_updating || Options.Items[e.Index].Tag is not ChoiceItem<TKey> choice) return;
            if (!choice.Enabled) { e.NewValue = e.CurrentValue; return; }
            if (e.NewValue == CheckState.Checked) _selected.Add(choice.Key); else _selected.Remove(choice.Key);
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        };
        Options.SelectedIndexChanged += (_, _) =>
        {
            if (_multiple || _updating || Options.SelectedItems.Count == 0 || Options.SelectedItems[0].Tag is not ChoiceItem<TKey> { Enabled: true } choice) return;
            _selected.Clear(); _selected.Add(choice.Key); SelectionChanged?.Invoke(this, EventArgs.Empty);
        };
        Rebuild();
    }
    public IReadOnlyList<TKey> SelectedKeys => _choices.Where(choice => _selected.Contains(choice.Key)).Select(choice => choice.Key).ToArray();
    public void SelectKey(TKey key, bool selected = true)
    {
        if (!_choices.Any(choice => EqualityComparer<TKey>.Default.Equals(choice.Key, key) && choice.Enabled)) throw new ArgumentException("有効な選択肢のキーではありません。", nameof(key));
        if (!_multiple) _selected.Clear();
        if (selected) _selected.Add(key); else _selected.Remove(key);
        Rebuild(); SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
    public void ClearSelection() { _selected.Clear(); Rebuild(); SelectionChanged?.Invoke(this, EventArgs.Empty); }
    private void Rebuild()
    {
        _updating = true; Options.BeginUpdate();
        try
        {
            Options.Items.Clear(); Options.Groups.Clear();
            var groups = new Dictionary<string, ListViewGroup>();
            foreach (var choice in _choices.Where(choice => (choice.Text + " " + choice.Group).Contains(Query.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)))
            {
                if (!groups.TryGetValue(choice.Group, out var group)) { group = new ListViewGroup(choice.Group); groups.Add(choice.Group, group); Options.Groups.Add(group); }
                var item = new ListViewItem(choice.Text, group) { Tag = choice, Checked = _selected.Contains(choice.Key), Selected = _selected.Contains(choice.Key), ForeColor = choice.Enabled ? ForeColor : SystemColors.GrayText };
                Options.Items.Add(item);
            }
        }
        finally { Options.EndUpdate(); _updating = false; }
    }
    public override Size GetPreferredSize(Size proposedSize) => new(proposedSize.Width > 0 ? proposedSize.Width : 300, Math.Max(160, Height));
}
