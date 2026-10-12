namespace Tansu.Forms.Components;

public static partial class Field
{
    public static InputField<string> IpAddress(string label, string initialValue = "", IpAddressVersion version = IpAddressVersion.Any)
        => Text(label, initialValue).IpAddress(version);

    /// <summary>Inclusive calendar-day range with built-in start/end order validation.</summary>
    public static InputField<DateRange> DateRange(string label, DateTime? start = null, DateTime? end = null)
    {
        var editor = new DateRangeEditor(new DateRange(start ?? DateTime.Today, end ?? start ?? DateTime.Today));
        editor.StartPicker.AccessibleName = $"{label}（開始）";
        editor.EndPicker.AccessibleName = $"{label}（終了）";
        var field = Custom(label, editor, () => editor.Value, value => editor.Value = value);
        editor.ValueChanged += (_, _) => field.NotifyValueChanged();
        return field.ValidateWith(value => value.IsValid, "開始日は終了日以前にしてください。");
    }
}
