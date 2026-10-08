namespace WinformsUI;

public static class Field
{
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

    public static InputField<string> Select(string label, params string[] choices)
    {
        var editor = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        editor.Items.AddRange(choices);
        if (choices.Length > 0) editor.SelectedIndex = 0;
        var field = new InputField<string>(label, editor, () => editor.SelectedItem as string ?? string.Empty,
            value => editor.SelectedItem = value);
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
