using System.Linq.Expressions;
using System.Reflection;

namespace Tansu.Forms.Components;

public static partial class Elements
{
    public static ComboBox ComboBox(params string[] choices) => ComboBox((IEnumerable<string>)choices);

    public static ComboBox ComboBox(IEnumerable<string> choices, int selectedIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(choices);
        return ComboBox(choices.Select(text => (Key: text, Value: text)), selectedIndex);
    }

    public static ComboBox ComboBox<TKey>(params (TKey Key, string Value)[] choices) where TKey : notnull
        => ComboBox((IEnumerable<(TKey Key, string Value)>)choices);

    /// <summary>Show Value and return Key through the native SelectedValue property.</summary>
    public static ComboBox ComboBox<TKey>(IEnumerable<(TKey Key, string Value)> choices, int selectedIndex = 0) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(choices);
        var items = choices.Select(pair => new ComboBoxOption<TKey>(pair.Key, pair.Value)).ToList();
        bool uniqueLabels = ValidateChoices(items, item => item.Key, item => item.Value);
        return CreateComboBox(items, nameof(ComboBoxOption<TKey>.Value), nameof(ComboBoxOption<TKey>.Key), selectedIndex, uniqueLabels);
    }

    public static ComboBox ComboBox<TKey>(IEnumerable<KeyValuePair<TKey, string>> choices, int selectedIndex = 0) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(choices);
        return ComboBox(choices.Select(pair => (pair.Key, pair.Value)), selectedIndex);
    }

    /// <summary>Bind original model instances using direct public properties for display and key.</summary>
    public static ComboBox ComboBox<TItem, TKey>(IEnumerable<TItem> choices,
        Expression<Func<TItem, string>> display, Expression<Func<TItem, TKey>> key, int selectedIndex = 0) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(display); ArgumentNullException.ThrowIfNull(key);
        string displayMember = PropertyName(display), valueMember = PropertyName(key);
        var items = choices.ToList();
        bool uniqueLabels = ValidateChoices(items, key.Compile(), display.Compile());
        return CreateComboBox(items, displayMember, valueMember, selectedIndex, uniqueLabels);
    }

    private static string PropertyName<TItem, TValue>(Expression<Func<TItem, TValue>> selector)
    {
        Expression body = selector.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } convert) body = convert.Operand;
        if (body is MemberExpression { Member: PropertyInfo property } member && member.Expression == selector.Parameters[0]
            && property.GetMethod?.IsPublic == true && property.GetIndexParameters().Length == 0)
            return property.Name;
        throw new ArgumentException("表示名とキーには x => x.Name のような直接の公開プロパティを指定してください。", nameof(selector));
    }

    private static bool ValidateChoices<TItem, TKey>(List<TItem> items, Func<TItem, TKey> key, Func<TItem, string> display) where TKey : notnull
    {
        var keys = new HashSet<TKey>();
        var labels = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        bool uniqueLabels = true;
        foreach (var item in items)
        {
            if (item is null) throw new ArgumentException("選択肢にnullを含めることはできません。", nameof(items));
            var value = key(item);
            string text = display(item);
            if (value is null || text is null) throw new ArgumentException("キーと表示名にnullは指定できません。", nameof(items));
            if (!keys.Add(value)) throw new ArgumentException($"選択肢のキー '{value}' が重複しています。", nameof(items));
            if (!labels.Add(text)) uniqueLabels = false;
        }
        return uniqueLabels;
    }

    private static ComboBox CreateComboBox<TItem>(List<TItem> items, string displayMember, string valueMember, int selectedIndex, bool uniqueLabels)
    {
        if (selectedIndex < -1 || selectedIndex >= items.Count && !(items.Count == 0 && selectedIndex == 0))
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList, Width = 220,
            IntegralHeight = true, MaxDropDownItems = 12,
            AutoCompleteSource = AutoCompleteSource.ListItems, AutoCompleteMode = uniqueLabels ? AutoCompleteMode.SuggestAppend : AutoCompleteMode.None,
            BindingContext = new BindingContext(), DisplayMember = displayMember, ValueMember = valueMember
        };
        try
        {
            combo.DataSource = items;
            combo.SelectedIndex = items.Count == 0 ? -1 : selectedIndex;
            return combo;
        }
        catch { combo.Dispose(); throw; }
    }
}

internal sealed record ComboBoxOption<TKey>(TKey Key, string Value);
