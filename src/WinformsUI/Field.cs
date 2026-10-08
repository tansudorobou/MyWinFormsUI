namespace WinformsUI;

public static class Field
{
    /// <summary>Wrap a native/custom editor with the same label, validation and help behavior.</summary>
    public static InputField<T> Custom<T>(string label, Control editor, Func<T> read, Action<T> write, bool multiline = false)
    {
        ArgumentNullException.ThrowIfNull(editor); ArgumentNullException.ThrowIfNull(read); ArgumentNullException.ThrowIfNull(write);
        return new InputField<T>(label, editor, read, write, multiline);
    }

    public static InputField<string> Text(string label, string initialValue = "") => CreateText(label, initialValue, false);
    public static InputField<string> Multiline(string label, string initialValue = "") => CreateText(label, initialValue, true);

    private static InputField<string> CreateText(string label, string value, bool multiline)
    {
        var editor = new TextBox { Text = value, Multiline = multiline, ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None };
        var field = new InputField<string>(label, editor, () => editor.Text, value => editor.Text = value, multiline);
        editor.TextChanged += (_, _) => field.NotifyValueChanged();
        return field;
    }

    public static InputField<decimal> Number(string label, decimal min = 0, decimal max = 1_000_000,
        int decimalPlaces = 0, decimal? initialValue = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(max, min);
        ArgumentOutOfRangeException.ThrowIfNegative(decimalPlaces);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(decimalPlaces, 28);
        decimal startingValue = initialValue ?? min;
        ArgumentOutOfRangeException.ThrowIfLessThan(startingValue, min);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startingValue, max);
        var editor = new NumericUpDown
        {
            Minimum = min, Maximum = max, DecimalPlaces = decimalPlaces,
            Value = startingValue, ThousandsSeparator = true
        };
        var field = new InputField<decimal>(label, editor, () => editor.Value, value => editor.Value = value);
        editor.ValueChanged += (_, _) => field.NotifyValueChanged();
        return field;
    }

    public static InputField<DateTime> Date(string label, DateTime? initialValue = null)
    {
        var editor = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = initialValue ?? DateTime.Today };
        var field = new InputField<DateTime>(label, editor, () => editor.Value.Date, value => editor.Value = value);
        editor.ValueChanged += (_, _) => field.NotifyValueChanged();
        return field;
    }

    public static InputField<DateTime?> DateText(string label, DateTime? initialValue = null)
    {
        var editor = new TextBox { Text = initialValue?.ToString("yyyy/MM/dd") ?? "", PlaceholderText = "今日、明日、来週月曜、yyyy/MM/dd" };
        var field = Custom<DateTime?>(label, editor,
            () => NaturalDates.TryParse(editor.Text, out var date) ? date : null,
            value => editor.Text = value?.ToString("yyyy/MM/dd") ?? "");
        field.ValidateWith(value => value.HasValue || string.IsNullOrWhiteSpace(editor.Text), "日付の形式を確認してください。");
        editor.TextChanged += (_, _) => field.NotifyValueChanged(); return field;
    }

    public static InputField<string> Select(string label) => Select(label, Array.Empty<(string Key, string Value)>());

    public static InputField<string> Select(string label, params string[] choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        return Select(label, choices.Select(value => (Key: value, Value: value)).ToArray());
    }

    public static InputField<string> Select(string label, IEnumerable<KeyValuePair<string, string>> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        return Select(label, choices.Select(pair => (pair.Key, pair.Value)).ToArray());
    }

    /// <summary>Display each pair's Value and read/write its Key through the field's Value.</summary>
    public static InputField<string> Select(string label, params (string Key, string Value)[] choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var choice in choices)
        {
            ArgumentNullException.ThrowIfNull(choice.Key);
            ArgumentNullException.ThrowIfNull(choice.Value);
            if (!keys.Add(choice.Key)) throw new ArgumentException($"選択肢のキー '{choice.Key}' が重複しています。", nameof(choices));
        }
        var editor = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = nameof(SelectOption.Value)
        };
        editor.Items.AddRange(choices.Select(choice => new SelectOption(choice.Key, choice.Value)).ToArray());
        if (choices.Length > 0) editor.SelectedIndex = 0;
        var field = new InputField<string>(label, editor,
            () => (editor.SelectedItem as SelectOption)?.Key ?? string.Empty,
            key =>
            {
                var option = editor.Items.OfType<SelectOption>().FirstOrDefault(option => string.Equals(option.Key, key, StringComparison.Ordinal));
                if (option is null)
                {
                    if (editor.Items.Count == 0 && key == string.Empty) { editor.SelectedIndex = -1; return; }
                    throw new ArgumentException($"選択肢にキー '{key}' がありません。", nameof(key));
                }
                editor.SelectedItem = option;
            });
        editor.SelectedIndexChanged += (_, _) => field.NotifyValueChanged();
        return field;
    }

    public static InputField<bool> Check(string label, bool initialValue = false)
    {
        var editor = new CheckBox { Checked = initialValue, Text = "はい", AutoSize = true };
        var field = new InputField<bool>(label, editor, () => editor.Checked, value => editor.Checked = value);
        editor.CheckedChanged += (_, _) => field.NotifyValueChanged();
        return field;
    }
}

// Null is used only for the search panel's unfiltered "all" option; ordinary keys may include "".
internal sealed record SelectOption(string? Key, string Value)
{
    public override string ToString() => Value;
}
