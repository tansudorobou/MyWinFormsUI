using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using Tansu.Forms.Execution;

namespace Tansu.Forms.Components;

public sealed class ModelFormChangedEventArgs(string? propertyName) : EventArgs
{
    /// <summary>The model property name; null indicates a complete load or reset.</summary>
    public string? PropertyName { get; } = propertyName;
}

/// <summary>DisplayName-driven form with native editors, DataAnnotations, dirty state and reset.</summary>
public sealed class ModelForm<T> : UserControl, INotifyPropertyChanged where T : class, new()
{
    private static readonly MethodInfo CloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private readonly AutoGridPanel _layout = Tansu.Forms.Layout.Layout.AutoGrid();
    private readonly Dictionary<string, Binding> _bindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> _original = new(StringComparer.Ordinal);
    private readonly List<string> _errors = [];
    private readonly Dictionary<string, List<string>> _fieldErrors = new(StringComparer.Ordinal);
    private T _originalModel;
    private bool _updating, _validateOnChange, _isValidated, _lastDirty, _lastValidated;
    private (string? Field, string Message)[] _lastIssues = [];
    public ModelForm(T? model = null)
    {
        _originalModel = model ?? new T();
        _layout.Dock = DockStyle.Fill; Controls.Add(_layout); Margin = Padding.Empty;
        foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(typeof(T)))
        {
            var info = typeof(T).GetProperty(property.Name);
            if (!property.IsBrowsable || property.IsReadOnly || info?.SetMethod?.IsPublic != true || info.GetIndexParameters().Length > 0) continue;
            AddProperty(property);
        }
        LoadModel(_originalModel);
    }
    public IReadOnlyDictionary<string, Control> Fields => _bindings.ToDictionary(pair => pair.Key, pair => (Control)pair.Value.Field);
    public IReadOnlyList<string> Errors => _errors.AsReadOnly();
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ErrorsByField => _fieldErrors.ToDictionary(pair => pair.Key,
        pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray()), StringComparer.Ordinal);
    public bool HasErrors => _errors.Count > 0;
    public bool IsValidated => _isValidated;
    [DefaultValue(false)]
    public bool ValidateOnChange
    {
        get => _validateOnChange;
        set { _validateOnChange = value; if (value) { ReadModel(out _); PublishState(); } }
    }
    public event EventHandler<ModelFormChangedEventArgs>? ValueChanged;
    public event EventHandler? IsDirtyChanged;
    public event EventHandler? ValidationChanged;
    public event EventHandler? StateChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool IsDirty => _bindings.Any(pair =>
    {
        try { return !Equals(pair.Value.Read(), _original[pair.Key]); }
        catch (Exception error) when (error is FormatException or ArgumentException or OverflowException or NotSupportedException) { return true; }
    });

    private void AddProperty(PropertyDescriptor property)
    {
        Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        bool nullable = Nullable.GetUnderlyingType(property.PropertyType) is not null;
        Control editor;
        Func<object?> read;
        Action<object?> write;
        bool multiline = false;
        if (property.Attributes.OfType<ChoicesAttribute>().FirstOrDefault() is { } source)
        {
            if (type != typeof(string)) throw new NotSupportedException("Choicesは文字列プロパティに指定してください。");
            var choices = Elements.ComboBox(source.GetChoices(), selectedIndex: -1);
            editor = choices;
            read = () => choices.SelectedValue as string ?? "";
            write = value => { if (value is null || Equals(value, "")) choices.SelectedIndex = -1; else choices.SelectedValue = value; };
        }
        else if (type == typeof(bool))
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
        editor.Name = property.Name;
        field.ValueChanged += (_, _) => FieldChanged(property.Name);
        _bindings.Add(property.Name, new Binding(property, field, read, write)); _layout.Add(field);
    }

    public void LoadModel(T model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _updating = true;
        try
        {
            _originalModel = CloneModel(model); _original.Clear(); ClearValidationCore();
            foreach (var (name, binding) in _bindings)
            {
                binding.Write(binding.Property.GetValue(model));
                _original[name] = binding.Read();
            }
        }
        finally { _updating = false; }
        ValueChanged?.Invoke(this, new ModelFormChangedEventArgs(null)); PublishState(true);
    }
    public void Reset()
    {
        _updating = true;
        try { foreach (var (name, binding) in _bindings) binding.Write(_original[name]); ClearValidationCore(); }
        finally { _updating = false; }
        ValueChanged?.Invoke(this, new ModelFormChangedEventArgs(null)); PublishState(true);
    }
    public bool TryRead(out T? model)
    {
        bool valid = ReadModel(out model); PublishState(); return valid;
    }

    /// <summary>Validate and mark the current inputs as the saved/reset baseline.</summary>
    public bool AcceptChanges()
    {
        if (!ReadModel(out var model)) { PublishState(); return false; }
        _originalModel = CloneModel(model!);
        foreach (var (name, binding) in _bindings) _original[name] = binding.Read();
        PublishState(); return true;
    }

    public void ClearValidation() { ClearValidationCore(); PublishState(); }
    /// <summary>Display action/business validation without replacing the current editor values.</summary>
    public void ApplyErrors(IReadOnlyDictionary<string, IReadOnlyList<string>> errors, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ClearValidationCore(); _isValidated = true;
        foreach (var (name, issues) in errors)
            foreach (string issue in issues) AddError(issue, [name]);
        if (_errors.Count == 0 && !string.IsNullOrWhiteSpace(message)) AddError(message, []);
        PublishState();
    }
    public bool FocusFirstError() => _fieldErrors.Keys.FirstOrDefault(name => _bindings.ContainsKey(name)) is { } name
        && _bindings[name].Field.Editor.Focus();

    private void FieldChanged(string propertyName)
    {
        if (_updating) return;
        if (_validateOnChange) ReadModel(out _); else ClearValidationCore();
        ValueChanged?.Invoke(this, new ModelFormChangedEventArgs(propertyName)); PublishState(true);
    }

    private void ClearValidationCore()
    {
        _errors.Clear(); _fieldErrors.Clear(); _isValidated = false;
        foreach (var binding in _bindings.Values) binding.Field.ClearValidation();
    }

    private bool ReadModel(out T? model)
    {
        ClearValidationCore(); _isValidated = true; var candidate = CloneModel(_originalModel);
        foreach (var binding in _bindings.Values)
        {
            try
            {
                object? value = binding.Read();
                if (!binding.Field.ValidateValue()) AddError(binding.Field.ErrorMessage!, [binding.Property.Name]);
                else binding.Property.SetValue(candidate, value);
            }
            catch (Exception error) when (error is FormatException or ArgumentException or OverflowException or NotSupportedException)
            {
                string message = $"{binding.Property.DisplayName}: 入力形式を確認してください。";
                AddError(message, [binding.Property.Name]);
            }
        }
        if (_errors.Count == 0)
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(candidate, new ValidationContext(candidate), results, true);
            foreach (var result in results)
            {
                AddError(result.ErrorMessage ?? "入力内容を確認してください。", result.MemberNames);
            }
        }
        model = _errors.Count == 0 ? candidate : null;
        return model is not null;
    }

    private void AddError(string message, IEnumerable<string> names)
    {
        _errors.Add(message);
        foreach (string name in names)
        {
            if (!_bindings.TryGetValue(name, out var binding)) continue;
            if (!_fieldErrors.TryGetValue(name, out var errors)) { errors = []; _fieldErrors.Add(name, errors); }
            errors.Add(message); binding.Field.SetError(string.Join(Environment.NewLine, errors));
        }
    }

    private void PublishState(bool force = false)
    {
        bool dirty = IsDirty;
        var issues = _errors.Select(message => ((string?)null, message))
            .Concat(_fieldErrors.SelectMany(pair => pair.Value.Select(message => ((string?)pair.Key, message)))).ToArray();
        bool dirtyChanged = dirty != _lastDirty;
        bool validationChanged = _isValidated != _lastValidated || !issues.SequenceEqual(_lastIssues);
        _lastDirty = dirty; _lastValidated = _isValidated; _lastIssues = issues;
        if (dirtyChanged) { IsDirtyChanged?.Invoke(this, EventArgs.Empty); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDirty))); }
        if (validationChanged)
        {
            ValidationChanged?.Invoke(this, EventArgs.Empty);
            foreach (string name in new[] { nameof(Errors), nameof(ErrorsByField), nameof(HasErrors), nameof(IsValidated) })
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        if (force || dirtyChanged || validationChanged) StateChanged?.Invoke(this, EventArgs.Empty);
    }
    public override Size GetPreferredSize(Size proposedSize) => _layout is null ? base.GetPreferredSize(proposedSize) : _layout.GetPreferredSize(proposedSize);
    // Preserve hidden properties and read-only/private state; references remain shared with the loaded model.
    private static T CloneModel(T model) => (T)CloneMethod.Invoke(model, null)!;
    private sealed record Binding(PropertyDescriptor Property, InputField<object?> Field, Func<object?> Read, Action<object?> Write);
    private sealed record ModelChoice(object? Value, string Text) { public override string ToString() => Text; }
}
