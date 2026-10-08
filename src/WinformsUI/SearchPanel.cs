using UiLayout = WinformsUI.Layout;

namespace WinformsUI;

/// <summary>Code-defined search fields that filter model instances in a connected DataGrid.</summary>
public sealed class SearchPanel : UserControl
{
    private readonly AutoGridPanel _fields = UiLayout.AutoGrid();
    private readonly List<SearchCondition> _conditions = [];
    private readonly Dictionary<string, InputField<string>> _selectFields = new(StringComparer.Ordinal);
    private readonly StackPanel _content;
    private DataGrid? _grid;

    public SearchPanel()
    {
        Margin = Padding.Empty;
        TabStop = false;
        SearchButton = Ui.Button("検索", () => ApplySearch());
        ClearButton = Ui.Button("条件をクリア", ClearSearch);
        _content = UiLayout.Stack(_fields, UiLayout.Row(SearchButton, ClearButton));
        _content.Dock = DockStyle.Fill;
        Controls.Add(_content);
    }

    public Button SearchButton { get; }
    public Button ClearButton { get; }
    public IReadOnlyList<Control> Fields => _fields.Controls.Cast<Control>().ToArray();

    public SearchPanel AddText(string propertyName, string label)
    {
        var field = Field.Text(label);
        AddCondition(new(propertyName, typeof(string), grid =>
        {
            string text = field.Value.Trim();
            if (text.Length == 0) return null;
            var property = grid.FindProperty(propertyName);
            return item => property.GetValue(item) is string value
                && value.Contains(text, StringComparison.CurrentCultureIgnoreCase);
        }, () => field.Value = "", field.ValidateValue));
        _fields.Add(field);
        field.Editor.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            ApplySearch();
        };
        return this;
    }

    public SearchPanel AddSelect(string propertyName, string label) => AddSelect(propertyName, label, Array.Empty<(string Key, string Value)>());

    public SearchPanel AddSelect(string propertyName, string label, params string[] choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        return AddSelect(propertyName, label, choices.Select(value => (Key: value, Value: value)).ToArray());
    }

    public SearchPanel AddSelect(string propertyName, string label, IEnumerable<KeyValuePair<string, string>> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        return AddSelect(propertyName, label, choices.Select(pair => (pair.Key, pair.Value)).ToArray());
    }

    /// <summary>Display Value, return Key through the field, and match model properties against Key.</summary>
    public SearchPanel AddSelect(string propertyName, string label, params (string Key, string Value)[] choices)
    {
        if (_selectFields.ContainsKey(propertyName))
            throw new ArgumentException($"プロパティ '{propertyName}' の選択欄はすでに追加されています。", nameof(propertyName));
        var field = Field.Select(label, choices);
        var editor = (ComboBox)field.Editor;
        editor.Items.Insert(0, new SelectOption(null, "すべて"));
        editor.SelectedIndex = 0;
        AddCondition(new(propertyName, typeof(string), grid =>
        {
            if (editor.SelectedIndex <= 0) return null;
            string selected = field.Value;
            var property = grid.FindProperty(propertyName);
            return item => property.GetValue(item) is string value
                && string.Equals(value, selected, StringComparison.Ordinal);
        }, () => { editor.SelectedIndex = 0; field.ClearValidation(); }, field.ValidateValue));
        _fields.Add(field);
        _selectFields.Add(propertyName, field);
        return this;
    }

    /// <summary>Return the selected key; null represents the unfiltered "all" option.</summary>
    public string? GetSelectedKey(string propertyName)
    {
        if (!_selectFields.TryGetValue(propertyName, out var field))
            throw new ArgumentException($"プロパティ '{propertyName}' の選択欄がありません。", nameof(propertyName));
        return ((ComboBox)field.Editor).SelectedIndex <= 0 ? null : field.Value;
    }

    public SearchPanel AddDateRange(string propertyName, string label, DateTime start, DateTime end)
    {
        var from = Field.Date($"{label}（開始）", start);
        var to = Field.Date($"{label}（終了）", end);
        from.ValidateWith(value => value <= to.Value, "開始日は終了日以前にしてください。");
        AddCondition(new(propertyName, typeof(DateTime), grid =>
        {
            DateTime startDate = from.Value;
            DateTime endDate = to.Value;
            var property = grid.FindProperty(propertyName);
            return item => property.GetValue(item) is DateTime date && date.Date >= startDate && date.Date <= endDate;
        }, () => { from.Value = start; to.Value = end; from.ClearValidation(); }, from.ValidateValue));
        _fields.Add(from, to);
        return this;
    }

    private void AddCondition(SearchCondition condition)
    {
        if (_grid is not null) ValidateProperty(_grid, condition);
        _conditions.Add(condition);
    }

    private static void ValidateProperty(DataGrid grid, SearchCondition condition)
    {
        var property = grid.FindProperty(condition.PropertyName);
        Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (type != condition.PropertyType)
            throw new ArgumentException($"検索対象 '{condition.PropertyName}' は {condition.PropertyType.Name} 型である必要があります。");
    }

    public SearchPanel Connect(DataGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (!grid.HasDataSource) throw new InvalidOperationException("DataGrid.SetDataを先に呼び出してください。");
        foreach (var condition in _conditions) ValidateProperty(grid, condition);
        _grid = grid;
        return this;
    }

    public bool ApplySearch()
    {
        if (_grid is null) throw new InvalidOperationException("Connectを先に呼び出してください。");
        bool valid = true;
        foreach (var condition in _conditions) valid &= condition.Validate();
        if (!valid) return false;
        var predicates = _conditions.Select(condition => condition.CreatePredicate(_grid))
            .OfType<Func<object, bool>>().ToArray();
        _grid.ApplySearch(item => predicates.All(predicate => predicate(item)));
        return true;
    }

    public void ClearSearch()
    {
        foreach (var condition in _conditions) condition.Clear();
        _grid?.ClearFilter();
    }

    public override Size GetPreferredSize(Size proposedSize) => _content is null
        ? base.GetPreferredSize(proposedSize) : _content.GetPreferredSize(proposedSize);

    private sealed record SearchCondition(string PropertyName, Type PropertyType,
        Func<DataGrid, Func<object, bool>?> CreatePredicate, Action Clear, Func<bool> Validate);
}
