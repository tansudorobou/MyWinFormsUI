using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace WinformsUI;

/// <summary>DisplayName-driven form with native editors, DataAnnotations, dirty state and reset.</summary>
public sealed class ModelForm<T> : UserControl where T : class, new()
{
    private readonly AutoGridPanel _layout = WinformsUI.Layout.AutoGrid();
    private readonly Dictionary<string, Binding> _bindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> _original = new(StringComparer.Ordinal);
    private readonly List<string> _errors = [];
    public ModelForm(T? model = null)
    {
        _layout.Dock = DockStyle.Fill; Controls.Add(_layout); Margin = Padding.Empty;
        foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(typeof(T)))
        {
            var info = typeof(T).GetProperty(property.Name);
            if (!property.IsBrowsable || property.IsReadOnly || info?.SetMethod?.IsPublic != true || info.GetIndexParameters().Length > 0) continue;
            AddProperty(property);
        }
        LoadModel(model ?? new T());
    }
    public IReadOnlyDictionary<string, Control> Fields => _bindings.ToDictionary(pair => pair.Key, pair => (Control)pair.Value.Field);
    public IReadOnlyList<string> Errors => _errors.AsReadOnly();
    public bool IsDirty => _bindings.Any(pair =>
    {
        try { return !Equals(pair.Value.Read(), _original[pair.Key]); }
        catch (Exception error) when (error is FormatException or ArgumentException or OverflowException) { return true; }
    });

    private void AddProperty(PropertyDescriptor property)
    {
        Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        bool nullable = Nullable.GetUnderlyingType(property.PropertyType) is not null;
        Control editor;
        Func<object?> read;
        Action<object?> write;
        bool multiline = false;
        if (type == typeof(bool))
        {
            var check = new CheckBox { ThreeState = nullable, AutoSize = true, Text = "はい" };
            editor = check;
            read = () => nullable && check.CheckState == CheckState.Indeterminate ? null : check.Checked;
            write = value => check.CheckState = value is null && nullable ? CheckState.Indeterminate : Equals(value, true) ? CheckState.Checked : CheckState.Unchecked;
        }
        else if (type == typeof(DateTime))
        {
            var date = new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = nullable };
            editor = date;
            read = () => nullable && !date.Checked ? null : date.Value.Date;
            write = value =>
            {
                date.Checked = value is not null;
                if (value is DateTime actual) date.Value = actual == DateTime.MinValue ? DateTime.Today : actual;
            };
        }
        else if (type.IsEnum)
        {
            var choices = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            if (nullable) choices.Items.Add(new ModelChoice(null, "未選択"));
            foreach (object value in Enum.GetValues(type))
            {
                string name = value.ToString()!;
                string label = type.GetField(name)?.GetCustomAttributes(typeof(DisplayNameAttribute), false).OfType<DisplayNameAttribute>().FirstOrDefault()?.DisplayName ?? name;
                choices.Items.Add(new ModelChoice(value, label));
            }
            editor = choices; read = () => (choices.SelectedItem as ModelChoice)?.Value;
            write = value => choices.SelectedItem = choices.Items.OfType<ModelChoice>().FirstOrDefault(choice => Equals(choice.Value, value));
        }
        else
        {
            multiline = property.Attributes.OfType<DataTypeAttribute>().Any(attribute => attribute.DataType == DataType.MultilineText);
            var text = new TextBox { Multiline = multiline, ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None };
            var converter = TypeDescriptor.GetConverter(type);
            if (type != typeof(string) && !converter.CanConvertFrom(typeof(string))) throw new NotSupportedException($"{property.Name} は自動フォーム非対応です。Browsable(false)で除外して独自のFieldを追加してください。");
            editor = text;
            read = () => type == typeof(string) ? text.Text : nullable && string.IsNullOrWhiteSpace(text.Text) ? null : converter.ConvertFromString(null, CultureInfo.CurrentCulture, text.Text);
            write = value => text.Text = value is null ? "" : type == typeof(string) ? (string)value : converter.ConvertToString(null, CultureInfo.CurrentCulture, value) ?? "";
        }
        var field = Field.Custom(property.DisplayName, editor, read, write, multiline);
        _bindings.Add(property.Name, new Binding(property, field, read, write)); _layout.Add(field);
    }

    public void LoadModel(T model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _errors.Clear(); _original.Clear();
        foreach (var (name, binding) in _bindings)
        {
            binding.Write(binding.Property.GetValue(model)); binding.Field.ClearValidation();
            _original[name] = binding.Read();
        }
    }
    public void Reset()
    {
        foreach (var (name, binding) in _bindings) { binding.Write(_original[name]); binding.Field.ClearValidation(); }
        _errors.Clear();
    }
    public bool TryRead(out T? model)
    {
        _errors.Clear(); var candidate = new T();
        foreach (var binding in _bindings.Values)
        {
            binding.Field.ClearValidation();
            try { binding.Property.SetValue(candidate, binding.Read()); }
            catch (Exception error) when (error is FormatException or ArgumentException or OverflowException or NotSupportedException)
            {
                string message = $"{binding.Property.DisplayName}: 入力形式を確認してください。";
                binding.Field.SetError(message); _errors.Add(message);
            }
        }
        if (_errors.Count == 0)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(candidate, new ValidationContext(candidate), results, true);
            foreach (var result in results)
            {
                string message = result.ErrorMessage ?? "入力内容を確認してください。"; _errors.Add(message);
                foreach (string name in result.MemberNames) if (_bindings.TryGetValue(name, out var binding)) binding.Field.SetError(message);
            }
        }
        model = _errors.Count == 0 ? candidate : null;
        return model is not null;
    }
    public override Size GetPreferredSize(Size proposedSize) => _layout is null ? base.GetPreferredSize(proposedSize) : _layout.GetPreferredSize(proposedSize);
    private sealed record Binding(PropertyDescriptor Property, InputField<object?> Field, Func<object?> Read, Action<object?> Write);
    private sealed record ModelChoice(object? Value, string Text) { public override string ToString() => Text; }
}
