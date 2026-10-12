using System.ComponentModel;

namespace Tansu.Forms.Components;

/// <summary>Inclusive calendar dates. Time components are ignored for validation and containment.</summary>
public readonly record struct DateRange(DateTime Start, DateTime End)
{
    public bool IsValid => Start.Date <= End.Date;
    public bool Contains(DateTime date) => date.Date >= Start.Date && date.Date <= End.Date;
}

/// <summary>Two native date pickers; a narrow editor stacks them vertically.</summary>
public sealed class DateRangeEditor : UserControl
{
    private readonly Label _separator = new() { Text = "〜", TextAlign = ContentAlignment.MiddleCenter, TabStop = false };
    private bool _writing;
    public DateTimePicker StartPicker { get; } = new() { Format = DateTimePickerFormat.Short, TabIndex = 0 };
    public DateTimePicker EndPicker { get; } = new() { Format = DateTimePickerFormat.Short, TabIndex = 1 };

    internal DateRangeEditor(DateRange value)
    {
        Margin = Padding.Empty; TabStop = false;
        Controls.AddRange([StartPicker, EndPicker, _separator]);
        try { Value = value; }
        catch { Dispose(); throw; }
        StartPicker.ValueChanged += PickerChanged; EndPicker.ValueChanged += PickerChanged;
        Size = GetPreferredSize(new Size(220, 0));
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateRange Value
    {
        get => new(StartPicker.Value.Date, EndPicker.Value.Date);
        set
        {
            DateTime start = value.Start.Date, end = value.End.Date;
            if (start < StartPicker.MinDate || start > StartPicker.MaxDate || end < EndPicker.MinDate || end > EndPicker.MaxDate)
                throw new ArgumentOutOfRangeException(nameof(value), "日付はカレンダーの選択可能な範囲内にしてください。");
            bool changed = Value != new DateRange(start, end);
            _writing = true;
            try { StartPicker.Value = start; EndPicker.Value = end; }
            finally { _writing = false; }
            if (changed) ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public event EventHandler? ValueChanged;
    private void PickerChanged(object? sender, EventArgs e) { if (!_writing) ValueChanged?.Invoke(this, EventArgs.Empty); }
    private int ScalePixels(int value) => (int)Math.Round(value * DeviceDpi / 96d);
    public override Size GetPreferredSize(Size proposedSize)
    {
        if (StartPicker is null) return base.GetPreferredSize(proposedSize);
        int width = proposedSize.Width > 0 ? proposedSize.Width : ScalePixels(220);
        int height = Math.Max(StartPicker.PreferredHeight, EndPicker.PreferredHeight);
        return new Size(width, width >= ScalePixels(320) ? height : height * 2 + ScalePixels(20));
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (StartPicker is null || _separator is null) return;
        int height = Math.Max(StartPicker.PreferredHeight, EndPicker.PreferredHeight);
        int gap = ScalePixels(20);
        if (ClientSize.Width >= ScalePixels(320))
        {
            int width = Math.Max(1, (ClientSize.Width - gap) / 2);
            StartPicker.SetBounds(0, 0, width, height);
            _separator.SetBounds(width, 0, gap, height);
            EndPicker.SetBounds(width + gap, 0, ClientSize.Width - width - gap, height);
        }
        else
        {
            StartPicker.SetBounds(0, 0, ClientSize.Width, height);
            _separator.SetBounds(0, height, ClientSize.Width, gap);
            EndPicker.SetBounds(0, height + gap, ClientSize.Width, height);
        }
    }
}
