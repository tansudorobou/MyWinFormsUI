using System.ComponentModel;

namespace WinformsUI;

public sealed class Pagination : UserControl
{
    private readonly Label _status = new() { AutoSize = true };
    private readonly FlowLayoutPanel _row;
    private int _totalItems;
    private int _pageSize;
    private int _page;
    public Button FirstButton { get; }
    public Button PreviousButton { get; }
    public Button NextButton { get; }
    public Button LastButton { get; }
    public event EventHandler? PageChanged;
    public Pagination(int pageSize = 10)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1); _pageSize = pageSize;
        FirstButton = Ui.Button("最初", () => Page = 0); PreviousButton = Ui.Button("前へ", () => Page--);
        NextButton = Ui.Button("次へ", () => Page++); LastButton = Ui.Button("最後", () => Page = Math.Max(0, PageCount - 1));
        _row = WinformsUI.Layout.Wrap(FirstButton, PreviousButton, _status, NextButton, LastButton);
        _row.Dock = DockStyle.Fill; Controls.Add(_row); Margin = Padding.Empty; RefreshButtons();
    }
    [DefaultValue(10)]
    public int PageSize
    {
        get => _pageSize;
        set { ArgumentOutOfRangeException.ThrowIfLessThan(value, 1); _pageSize = value; _page = 0; RefreshButtons(); PageChanged?.Invoke(this, EventArgs.Empty); }
    }
    [DefaultValue(0)]
    public int Page
    {
        get => _page;
        set
        {
            int page = Math.Clamp(value, 0, Math.Max(0, PageCount - 1));
            if (_page == page) return;
            _page = page; RefreshButtons(); PageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public int PageCount => _totalItems == 0 ? 0 : (_totalItems - 1) / _pageSize + 1;
    public int TotalItems => _totalItems;
    public void SetTotal(int total)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(total);
        _totalItems = total; _page = Math.Min(_page, Math.Max(0, PageCount - 1)); RefreshButtons();
    }
    private void RefreshButtons()
    {
        _status.Text = $"{(PageCount == 0 ? 0 : _page + 1)} / {PageCount} ページ · {_totalItems} 件";
        FirstButton.Enabled = PreviousButton.Enabled = _page > 0;
        NextButton.Enabled = LastButton.Enabled = _page + 1 < PageCount;
        LayoutInvalidation.NotifyAncestors(this);
    }
    public override Size GetPreferredSize(Size proposedSize) => _row is null ? base.GetPreferredSize(proposedSize) : _row.GetPreferredSize(proposedSize);
}
