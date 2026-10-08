using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace WinformsUI;

public enum GanttScale { Day, Week, Month }

/// <summary>Dates are inclusive calendar days; progress is a percentage from 0 to 100.</summary>
public sealed class GanttTask
{
    [DisplayName("ID")]
    public string Id { get; set; } = "";
    [DisplayName("作業")]
    public string Title { get; set; } = "";
    [DisplayName("開始日")]
    public DateTime Start { get; set; } = DateTime.Today;
    [DisplayName("終了日")]
    public DateTime End { get; set; } = DateTime.Today;
    [DisplayName("進捗 (%)")]
    public int Progress { get; set; }
    [DisplayName("担当・ライン")]
    public string Resource { get; set; } = "";
}

/// <summary>Code-first read-only scheduling view backed by a List. Edit the models and call RefreshData.</summary>
public sealed class GanttChart : ScrollableControl, IThemeAware
{
    private List<GanttTask> _items = [];
    private readonly ToolTip _tooltip = new();
    private GanttScale _scale;
    private DateTime _start = DateTime.Today.AddDays(-2), _end = DateTime.Today.AddDays(28);
    private GanttTask? _selected;
    private GanttTask? _hovered;
    private Color _bar = SystemColors.Highlight;
    private int _barRadius = 4;
    private int Header => Logical(52);
    private int RowHeight => Logical(38);
    private int LabelWidth => Logical(200);
    private int DayWidth => Logical(_scale switch { GanttScale.Day => 32, GanttScale.Week => 12, _ => 4 });
    private int Logical(int value) => (int)Math.Round(value * DeviceDpi / 96d);

    public GanttChart()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoScroll = true; TabStop = true; Size = new Size(800, 350); MinimumSize = new Size(300, 150);
        AccessibleName = "作業スケジュール"; AccessibleRole = AccessibleRole.Chart;
        UpdateExtent();
    }

    [DefaultValue(GanttScale.Day)]
    public GanttScale TimelineScale
    {
        get => _scale;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            DateTime left = _start.AddDays(Math.Max(0, -AutoScrollPosition.X / DayWidth));
            _scale = value; UpdateExtent(); ScrollToDate(left); Invalidate();
        }
    }
    public IReadOnlyList<GanttTask> Items => _items.AsReadOnly();
    public GanttTask? SelectedTask => _selected;
    public DateTime ViewStart => _start;
    public DateTime ViewEnd => _end;
    public event EventHandler? SelectionChanged;

    public GanttChart SetData(List<GanttTask> source)
    {
        ArgumentNullException.ThrowIfNull(source); Validate(source);
        _items = source; SetSelection(null); _hovered = null; _tooltip.SetToolTip(this, null);
        FitToTasks(); return this;
    }
    public void RefreshData()
    {
        Validate(_items);
        if (_selected is not null && !_items.Contains(_selected)) SetSelection(null);
        _hovered = null; _tooltip.SetToolTip(this, null); UpdateExtent(); Invalidate();
    }
    private static void Validate(List<GanttTask> items)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (items.Count > 100000) throw new ArgumentException("作業数は100000件以下にしてください。");
        foreach (var item in items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id)) throw new ArgumentException("各作業には重複しないIDを指定してください。");
            if (item.Start.Date > item.End.Date || item.Progress is < 0 or > 100) throw new ArgumentException("開始日・終了日の順序、進捗 (0〜100) を確認してください。");
        }
        if (items.Count > 0 && (items.Max(item => item.End.Date) - items.Min(item => item.Start.Date)).TotalDays > 36600)
            throw new ArgumentException("作業の全期間は100年以内にしてください。");
    }
    public void FitToTasks()
    {
        if (_items.Count == 0) SetViewRange(DateTime.Today.AddDays(-2), DateTime.Today.AddDays(28));
        else SetViewRange(_items.Min(item => item.Start.Date), _items.Max(item => item.End.Date));
        AutoScrollPosition = Point.Empty;
    }
    public void SetViewRange(DateTime start, DateTime end)
    {
        start = start.Date; end = end.Date;
        if (end < start || (end - start).TotalDays > 36600) throw new ArgumentException("表示期間は開始日以降、100年以内にしてください。");
        _start = start; _end = end; UpdateExtent(); Invalidate();
    }
    public void ScrollToDate(DateTime date)
    {
        int offset = (int)Math.Clamp((date.Date - _start).TotalDays, 0, (_end - _start).TotalDays);
        AutoScrollPosition = new Point(offset * DayWidth, -AutoScrollPosition.Y); Invalidate();
    }
    public void SelectTask(string? id)
    {
        var task = id is null ? null : _items.Find(item => item.Id == id) ?? throw new ArgumentException("作業IDがありません。", nameof(id));
        SetSelection(task);
        if (task is null) return;
        int row = _items.IndexOf(task) * RowHeight;
        int top = -AutoScrollPosition.Y, available = Math.Max(RowHeight, ClientSize.Height - Header);
        if (row < top) AutoScrollPosition = new Point(-AutoScrollPosition.X, row);
        else if (row + RowHeight > top + available) AutoScrollPosition = new Point(-AutoScrollPosition.X, row + RowHeight - available);
    }
    private void SetSelection(GanttTask? task)
    {
        if (ReferenceEquals(_selected, task)) return;
        _selected = task;
        AccessibleDescription = task is null ? "選択なし" : Describe(task);
        Invalidate(); SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
    private void UpdateExtent() => AutoScrollMinSize = new Size(LabelWidth + ((int)(_end - _start).TotalDays + 1) * DayWidth, Header + _items.Count * RowHeight);
    public Rectangle GetTaskBounds(string id)
    {
        int index = _items.FindIndex(item => item.Id == id);
        if (index < 0) throw new ArgumentException("作業IDがありません。", nameof(id));
        var item = _items[index];
        int left = LabelWidth + AutoScrollPosition.X + (int)(item.Start.Date - _start).TotalDays * DayWidth;
        int width = ((int)(item.End.Date - item.Start.Date).TotalDays + 1) * DayWidth;
        return new Rectangle(left + Logical(2), Header + AutoScrollPosition.Y + index * RowHeight + Logical(8), Math.Max(1, width - Logical(4)), RowHeight - Logical(16));
    }
    public GanttTask? HitTest(Point point)
    {
        if (!ClientRectangle.Contains(point) || point.Y < Header) return null;
        if (point.X >= LabelWidth && point.X - LabelWidth - AutoScrollPosition.X >= ((int)(_end - _start).TotalDays + 1) * DayWidth) return null;
        int index = (point.Y - Header - AutoScrollPosition.Y) / RowHeight;
        if (index < 0 || index >= _items.Count) return null;
        var task = _items[index];
        return point.X < LabelWidth || GetTaskBounds(task.Id).Contains(point) ? task : null;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        using var gridPen = new Pen(Blend(BackColor, ForeColor, .15f));
        using var headerBrush = new SolidBrush(Blend(BackColor, ForeColor, .06f));
        using var selectionBrush = new SolidBrush(Blend(BackColor, _bar, .18f));
        int firstRow = Math.Max(0, -AutoScrollPosition.Y / RowHeight);
        int lastRow = Math.Min(_items.Count, firstRow + Math.Max(1, (ClientSize.Height - Header) / RowHeight) + 2);
        var state = g.Save();
        int timelineWidth = Math.Max(0, Math.Min(ClientSize.Width - LabelWidth, AutoScrollPosition.X + ((int)(_end - _start).TotalDays + 1) * DayWidth));
        g.SetClip(new Rectangle(LabelWidth, Header, timelineWidth, Math.Max(0, ClientSize.Height - Header)));
        int firstDay = Math.Max(0, -AutoScrollPosition.X / DayWidth);
        int lastDay = Math.Min((int)(_end - _start).TotalDays, firstDay + ClientSize.Width / DayWidth + 1);
        for (int day = firstDay; day <= lastDay; day++)
        {
            DateTime date = _start.AddDays(day); int x = LabelWidth + AutoScrollPosition.X + day * DayWidth;
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) g.FillRectangle(headerBrush, x, Header, DayWidth, ClientSize.Height - Header);
            if (_scale == GanttScale.Day || _scale == GanttScale.Week && date.DayOfWeek == DayOfWeek.Monday || _scale == GanttScale.Month && date.Day == 1)
                g.DrawLine(gridPen, x, Header, x, ClientSize.Height);
        }
        for (int index = firstRow; index < lastRow; index++)
        {
            var item = _items[index]; var bounds = GetTaskBounds(item.Id);
            if (ReferenceEquals(item, _selected)) g.FillRectangle(selectionBrush, LabelWidth, Header + AutoScrollPosition.Y + index * RowHeight, ClientSize.Width, RowHeight);
            using var path = RoundedCorners.Path(bounds, Logical(_barRadius));
            using var barBrush = new SolidBrush(Blend(BackColor, _bar, .55f));
            g.FillPath(barBrush, path);
            var progressState = g.Save(); g.SetClip(path, CombineMode.Intersect);
            using var progressBrush = new SolidBrush(_bar);
            g.FillRectangle(progressBrush, bounds.X, bounds.Y, bounds.Width * item.Progress / 100f, bounds.Height);
            g.Restore(progressState);
            using var outline = new Pen(_bar, ReferenceEquals(item, _selected) ? 2 : 1); g.DrawPath(outline, path);
            if (bounds.Width > Logical(44))
            {
                DrawText(g, $"{item.Progress}%", bounds, ForeColor, TextFormatFlags.HorizontalCenter);
                var textState = g.Save(); g.SetClip(new RectangleF(bounds.X, bounds.Y, bounds.Width * item.Progress / 100f, bounds.Height), CombineMode.Intersect);
                Color onBar = _bar.R * .2126 + _bar.G * .7152 + _bar.B * .0722 < 140 ? Color.White : Color.Black;
                DrawText(g, $"{item.Progress}%", bounds, onBar, TextFormatFlags.HorizontalCenter); g.Restore(textState);
            }
            g.DrawLine(gridPen, LabelWidth, Header + AutoScrollPosition.Y + (index + 1) * RowHeight, ClientSize.Width, Header + AutoScrollPosition.Y + (index + 1) * RowHeight);
        }
        int todayX = LabelWidth + AutoScrollPosition.X + (int)(DateTime.Today - _start).TotalDays * DayWidth;
        if (DateTime.Today >= _start && DateTime.Today <= _end) { using var marker = new Pen(_bar, 2) { DashStyle = DashStyle.Dash }; g.DrawLine(marker, todayX, Header, todayX, ClientSize.Height); }
        g.Restore(state);

        g.FillRectangle(headerBrush, 0, 0, ClientSize.Width, Header);
        DrawText(g, "作業 / 担当・ライン", new Rectangle(Logical(8), 0, LabelWidth - Logical(16), Header), ForeColor);
        state = g.Save(); g.SetClip(new Rectangle(LabelWidth, 0, Math.Max(0, ClientSize.Width - LabelWidth), Header));
        // Only visible dates are enumerated; long schedules do not allocate a cell per day.
        for (int day = Math.Max(0, firstDay - 31); day <= lastDay; day++)
        {
            DateTime date = _start.AddDays(day); int x = LabelWidth + AutoScrollPosition.X + day * DayWidth;
            bool boundary = day == 0 || (_scale == GanttScale.Month ? date.Day == 1 : _scale == GanttScale.Week ? date.DayOfWeek == DayOfWeek.Monday : true);
            if (!boundary) continue;
            int days = _scale == GanttScale.Month ? DateTime.DaysInMonth(date.Year, date.Month) - date.Day + 1 : _scale == GanttScale.Week ? 7 - ((int)date.DayOfWeek + 6) % 7 : 1;
            var cell = new Rectangle(x, 0, days * DayWidth, Header);
            DrawText(g, date.ToString(_scale == GanttScale.Month ? "yyyy/MM" : _scale == GanttScale.Day || cell.Width < Logical(50) ? "MM\ndd" : "MM/dd"), cell, ForeColor, TextFormatFlags.HorizontalCenter);
            g.DrawLine(gridPen, x, 0, x, Header);
        }
        g.Restore(state);
        using var labelBrush = new SolidBrush(BackColor);
        g.FillRectangle(labelBrush, 0, Header, LabelWidth, Math.Max(0, ClientSize.Height - Header));
        state = g.Save(); g.SetClip(new Rectangle(0, Header, LabelWidth, Math.Max(0, ClientSize.Height - Header)));
        for (int index = firstRow; index < lastRow; index++)
        {
            var item = _items[index]; int y = Header + AutoScrollPosition.Y + index * RowHeight;
            if (ReferenceEquals(item, _selected)) g.FillRectangle(selectionBrush, 0, y, LabelWidth, RowHeight);
            string text = string.IsNullOrEmpty(item.Resource) ? item.Title : $"{item.Title} / {item.Resource}";
            DrawText(g, text, new Rectangle(Logical(8), y, LabelWidth - Logical(16), RowHeight), ForeColor);
            g.DrawLine(gridPen, 0, y + RowHeight, LabelWidth, y + RowHeight);
        }
        g.Restore(state);
        g.DrawLine(gridPen, 0, Header, ClientSize.Width, Header); g.DrawLine(gridPen, LabelWidth, 0, LabelWidth, ClientSize.Height);
        if (_items.Count == 0) DrawText(g, "作業がありません", new Rectangle(LabelWidth, Header, Math.Max(0, ClientSize.Width - LabelWidth), RowHeight), ForeColor);
    }
    private void DrawText(Graphics g, string text, Rectangle bounds, Color color, TextFormatFlags align = TextFormatFlags.Left)
        => TextRenderer.DrawText(g, text, Font, bounds, color, align | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping);
    private static Color Blend(Color from, Color to, float amount) => Color.FromArgb((int)(from.R + (to.R - from.R) * amount), (int)(from.G + (to.G - from.G) * amount), (int)(from.B + (to.B - from.B) * amount));
    private static string Describe(GanttTask task) => $"{task.Title} ({task.Resource})\n{task.Start:yyyy/MM/dd} 〜 {task.End:yyyy/MM/dd}\n進捗: {task.Progress}%";
    public void ApplyTheme(ThemeTemplate template)
    {
        _bar = template.ChartColors.Length == 0 ? SystemColors.Highlight : ThemeTemplate.ParseColor(template.ChartColors[0])!.Value;
        _barRadius = template.CornerRadius ?? 4; Invalidate();
    }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { Focus(); SetSelection(HitTest(e.Location)); } }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); var task = HitTest(e.Location);
        if (!ReferenceEquals(task, _hovered)) { _hovered = task; _tooltip.SetToolTip(this, task is null ? null : Describe(task)); }
    }
    protected override void OnScroll(ScrollEventArgs se) { base.OnScroll(se); Invalidate(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); UpdateExtent(); Invalidate(); }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e); if (_items.Count == 0) return;
        int index = _selected is null ? -1 : _items.IndexOf(_selected);
        int next = e.KeyCode switch { Keys.Down => Math.Min(_items.Count - 1, index + 1), Keys.Up => Math.Max(0, index - 1), Keys.Home => 0, Keys.End => _items.Count - 1, _ => -1 };
        if (next >= 0) { SelectTask(_items[next].Id); e.Handled = true; }
    }
    protected override void Dispose(bool disposing) { if (disposing) _tooltip.Dispose(); base.Dispose(disposing); }
}
