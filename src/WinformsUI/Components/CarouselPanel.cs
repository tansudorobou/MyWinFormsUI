using System.ComponentModel;

namespace WinformsUI;

public sealed class CarouselPanel : UserControl
{
    private readonly Panel _viewport = new();
    private readonly Label _position = new() { AutoSize = true };
    private readonly FlowLayoutPanel _actions;
    private readonly List<Control> _items = [];
    private int _index = -1;
    public Button PreviousButton { get; }
    public Button NextButton { get; }
    public event EventHandler? IndexChanged;

    public CarouselPanel(params Control[] items)
    {
        PreviousButton = Ui.Button("前へ", () => NavigateBy(-1));
        NextButton = Ui.Button("次へ", () => NavigateBy(1));
        _actions = WinformsUI.Layout.Row(PreviousButton, _position, NextButton);
        _actions.Dock = DockStyle.Bottom; _viewport.Dock = DockStyle.Fill;
        Controls.AddRange([_viewport, _actions]); MinimumSize = new Size(0, 180);
        Add(items);
    }
    [DefaultValue(false)]
    public bool Loop { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Index
    {
        get => _index;
        set
        {
            if (value < 0 || value >= _items.Count) throw new ArgumentOutOfRangeException(nameof(value));
            _index = value;
            for (int i = 0; i < _items.Count; i++) _items[i].Visible = i == value;
            _position.Text = $"{value + 1} / {_items.Count}";
            PreviousButton.Enabled = Loop || value > 0; NextButton.Enabled = Loop || value + 1 < _items.Count;
            IndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public int Count => _items.Count;
    public void Add(params Control[] items)
    {
        foreach (var item in items) { item.Dock = DockStyle.Fill; _items.Add(item); _viewport.Controls.Add(item); }
        if (_items.Count > 0) Index = Math.Max(0, _index);
        else { PreviousButton.Enabled = false; NextButton.Enabled = false; _position.Text = "0 / 0"; }
    }
    public bool NavigateBy(int delta)
    {
        if (_items.Count == 0) return false;
        int target = _index + delta;
        if (Loop) target = ((target % Count) + Count) % Count;
        else if (target < 0 || target >= Count) return false;
        Index = target; return true;
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Right)) return NavigateBy(1);
        if (keyData == (Keys.Control | Keys.Left)) return NavigateBy(-1);
        return base.ProcessCmdKey(ref msg, keyData);
    }
    public override Size GetPreferredSize(Size proposedSize) => new(proposedSize.Width > 0 ? proposedSize.Width : 320, 200);
}
