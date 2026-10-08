namespace WinformsUI;

/// <summary>Ordered range/multiple values using native TrackBars, with native keyboard navigation.</summary>
public sealed class MultiSlider : UserControl
{
    private readonly TrackBar[] _sliders;
    private readonly Label _summary = new() { AutoSize = true, Dock = DockStyle.Bottom };
    private bool _updating;
    public event EventHandler? ValuesChanged;
    public MultiSlider(int minimum, int maximum, params int[] values)
    {
        if (minimum > maximum || values.Length < 2 || values.Any(value => value < minimum || value > maximum) || !values.SequenceEqual(values.Order()))
            throw new ArgumentException("範囲内で昇順の値を2つ以上指定してください。", nameof(values));
        _sliders = values.Select(value => new TrackBar { Minimum = minimum, Maximum = maximum, Value = value, Dock = DockStyle.Top, TickStyle = TickStyle.None, Height = 35 }).ToArray();
        Controls.Add(_summary);
        for (int i = _sliders.Length - 1; i >= 0; i--)
        {
            int index = i;
            _sliders[i].AccessibleName = $"値 {i + 1}";
            _sliders[i].ValueChanged += (_, _) => Changed(index);
            Controls.Add(_sliders[i]);
        }
        Changed(0);
        Height = _sliders.Sum(slider => slider.Height) + _summary.PreferredHeight;
    }
    public IReadOnlyList<int> Values => _sliders.Select(slider => slider.Value).ToArray();
    public void SetValue(int index, int value)
    {
        if (index < 0 || index >= _sliders.Length) throw new ArgumentOutOfRangeException(nameof(index));
        _sliders[index].Value = value;
    }
    private void Changed(int index)
    {
        if (_updating) return;
        _updating = true;
        try
        {
            int value = _sliders[index].Value;
            if (index > 0) value = Math.Max(value, _sliders[index - 1].Value);
            if (index + 1 < _sliders.Length) value = Math.Min(value, _sliders[index + 1].Value);
            _sliders[index].Value = value;
            _summary.Text = string.Join(" – ", Values);
        }
        finally { _updating = false; }
        ValuesChanged?.Invoke(this, EventArgs.Empty);
    }
    public override Size GetPreferredSize(Size proposedSize) => new(proposedSize.Width > 0 ? proposedSize.Width : 320, Height);
}
