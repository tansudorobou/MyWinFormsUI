using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace WinformsUI;

public enum ChartKind { Bar, Line, Area, Pie, Donut, Radar, Radial }
public sealed record ChartSeries(string Name, IReadOnlyList<double> Values, Color? Color = null);

/// <summary>Small native chart renderer: axes, grid, series, legend, tooltips and keyboard selection.</summary>
public sealed class Chart : Control, IThemeAware
{
    private string[] _labels = [];
    private ChartSeries[] _series = [];
    private readonly HashSet<int> _hidden = [];
    private readonly ToolTip _tooltip = new();
    private readonly List<(RectangleF Bounds, int Index)> _legend = [];
    private readonly List<(RectangleF Bounds, int Index)> _points = [];
    private Color[] _colors = [];
    private ChartKind _kind;
    private int _selectedIndex;
    public Chart(ChartKind kind = ChartKind.Bar)
    {
        _kind = kind; SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        TabStop = true; AccessibleRole = AccessibleRole.Chart; MinimumSize = new Size(160, 160); Size = new Size(480, 280);
    }
    [DefaultValue(ChartKind.Bar)]
    public ChartKind Kind { get => _kind; set { ValidateData(value, _labels, _series); _kind = value; Invalidate(); } }
    [DefaultValue(false)]
    public bool Stacked { get; set; }
    public int PointCount => _labels.Length;
    public IReadOnlyList<ChartSeries> Series => Array.AsReadOnly(_series);

    public Chart SetData(IEnumerable<string> labels, params ChartSeries[] series)
    {
        var labelArray = labels.ToArray();
        var seriesArray = series.Select(item => item with { Values = Array.AsReadOnly(item.Values.ToArray()) }).ToArray();
        ValidateData(Kind, labelArray, seriesArray);
        _labels = labelArray; _series = seriesArray; _hidden.Clear(); _selectedIndex = 0; Invalidate(); return this;
    }
    private static void ValidateData(ChartKind kind, string[] labels, ChartSeries[] series)
    {
        if (series.Any(item => item.Values.Count != labels.Length || item.Values.Any(value => !double.IsFinite(value)))) throw new ArgumentException("ラベル数と同じ数の有限な値を各系列に指定してください。");
        if (kind is ChartKind.Pie or ChartKind.Donut or ChartKind.Radar or ChartKind.Radial && series.Any(item => item.Values.Any(value => value < 0))) throw new ArgumentException("円・レーダー・放射グラフには非負の値を指定してください。");
        if (kind == ChartKind.Radar && labels.Length is 1 or 2) throw new ArgumentException("レーダーには3点以上必要です。");
    }
    public void SetSeriesVisible(int index, bool visible)
    {
        if (index < 0 || index >= _series.Length) throw new ArgumentOutOfRangeException(nameof(index));
        if (visible) _hidden.Remove(index); else _hidden.Add(index); Invalidate();
    }
    public string GetPointDescription(int index)
    {
        if (index < 0 || index >= _labels.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return _labels[index] + "\n" + string.Join("\n", _series.Where((_, i) => !_hidden.Contains(i)).Select(series => $"{series.Name}: {series.Values[index]:N2}"));
    }
    public void ApplyTheme(ThemeTemplate template) { _colors = template.ChartColors.Select(text => ThemeTemplate.ParseColor(text)!.Value).ToArray(); Invalidate(); }
    private Color SeriesColor(int index) => _series[index].Color ?? Palette(index);
    private Color Palette(int index) => _colors.Length > 0 ? _colors[index % _colors.Length]
        : new[] { SystemColors.Highlight, Color.DarkCyan, Color.DarkOrange, Color.MediumPurple, Color.DarkGreen, Color.IndianRed }[index % 6];

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); _legend.Clear(); _points.Clear();
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        var visible = Enumerable.Range(0, _series.Length).Where(index => !_hidden.Contains(index)).ToArray();
        if (_labels.Length == 0 || visible.Length == 0)
        {
            TextRenderer.DrawText(g, "データがありません。", Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); return;
        }
        int x = 8;
        for (int i = 0; i < _series.Length; i++)
        {
            int width = Math.Min(180, TextRenderer.MeasureText(_series[i].Name, Font).Width + 24);
            var bounds = new RectangleF(x, 4, width, 24); _legend.Add((bounds, i));
            using var brush = new SolidBrush(_hidden.Contains(i) ? SystemColors.GrayText : SeriesColor(i));
            g.FillRectangle(brush, x, 11, 10, 10); TextRenderer.DrawText(g, _series[i].Name, Font, new Point(x + 16, 7), ForeColor); x += width;
        }
        var plot = new RectangleF(52, 38, Math.Max(1, Width - 72), Math.Max(1, Height - 78));
        if (Kind is ChartKind.Pie or ChartKind.Donut) DrawPie(g, plot, visible[0]);
        else if (Kind == ChartKind.Radar) DrawRadar(g, plot, visible);
        else if (Kind == ChartKind.Radial) DrawRadial(g, plot, visible[0]);
        else DrawCartesian(g, plot, visible);
    }

    private void DrawCartesian(Graphics g, RectangleF plot, int[] visible)
    {
        double low = Math.Min(0, visible.Min(index => _series[index].Values.Min()));
        double high = Math.Max(0, visible.Max(index => _series[index].Values.Max()));
        if (Stacked && Kind is ChartKind.Bar or ChartKind.Area)
        {
            high = Math.Max(high, Enumerable.Range(0, PointCount).Max(point => visible.Sum(index => Math.Max(0, _series[index].Values[point]))));
            low = Math.Min(low, Enumerable.Range(0, PointCount).Min(point => visible.Sum(index => Math.Min(0, _series[index].Values[point]))));
        }
        if (high <= low) high = low + 1;
        float Y(double value) => plot.Bottom - (float)((value - low) / (high - low)) * plot.Height;
        using var gridPen = new Pen(Color.FromArgb(70, ForeColor));
        for (int line = 0; line <= 4; line++)
        {
            double value = low + (high - low) * line / 4;
            float y = Y(value); g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            TextRenderer.DrawText(g, value.ToString("0.##"), Font, new Rectangle(0, (int)y - 10, 47, 20), ForeColor, TextFormatFlags.Right);
        }
        float slot = plot.Width / PointCount;
        for (int point = 0; point < PointCount; point++)
        {
            float center = plot.Left + slot * (point + .5f);
            if (PointCount <= 12 || point % Math.Max(1, PointCount / 8) == 0)
                TextRenderer.DrawText(g, _labels[point], Font, new Rectangle((int)(center - slot / 2), (int)plot.Bottom + 6, (int)slot, 30), ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
            _points.Add((new RectangleF(plot.Left + slot * point, plot.Top, slot, plot.Height), point));
        }
        var positive = new double[PointCount]; var negative = new double[PointCount];
        for (int s = 0; s < visible.Length; s++)
        {
            int index = visible[s]; Color color = SeriesColor(index);
            var linePoints = new PointF[PointCount]; var bases = new PointF[PointCount];
            for (int point = 0; point < PointCount; point++)
            {
                double value = _series[index].Values[point]; double bottom = 0;
                if (Stacked && Kind is ChartKind.Bar or ChartKind.Area)
                {
                    bottom = value >= 0 ? positive[point] : negative[point];
                    if (value >= 0) positive[point] += value; else negative[point] += value;
                }
                float center = plot.Left + slot * (point + .5f);
                linePoints[point] = new PointF(center, Y(value + bottom)); bases[point] = new PointF(center, Y(bottom));
                if (Kind == ChartKind.Bar)
                {
                    float width = slot * .75f / (Stacked ? 1 : visible.Length);
                    float left = plot.Left + point * slot + slot * .125f + (Stacked ? 0 : s * width);
                    float top = Math.Min(Y(value + bottom), Y(bottom));
                    using var brush = new SolidBrush(color); g.FillRectangle(brush, left, top, Math.Max(1, width - 2), Math.Max(1, Math.Abs(Y(value + bottom) - Y(bottom))));
                }
            }
            if (Kind is ChartKind.Line or ChartKind.Area)
            {
                if (Kind == ChartKind.Area && PointCount > 1)
                {
                    using var area = new SolidBrush(Color.FromArgb(80, color)); g.FillPolygon(area, linePoints.Concat(bases.Reverse()).ToArray());
                }
                using var pen = new Pen(color, 2); if (PointCount > 1) g.DrawLines(pen, linePoints);
                using var brush = new SolidBrush(color); foreach (var point in linePoints) g.FillEllipse(brush, point.X - 3, point.Y - 3, 6, 6);
            }
        }
    }

    private void DrawPie(Graphics g, RectangleF plot, int series)
    {
        double total = _series[series].Values.Sum(); if (total <= 0) return;
        float diameter = Math.Min(plot.Width, plot.Height);
        var circle = new RectangleF(plot.Left + (plot.Width - diameter) / 2, plot.Top, diameter, diameter);
        float angle = -90;
        for (int i = 0; i < PointCount; i++)
        {
            float sweep = (float)(_series[series].Values[i] / total * 360);
            using var brush = new SolidBrush(Palette(i)); g.FillPie(brush, circle.X, circle.Y, circle.Width, circle.Height, angle, sweep);
            float middle = (angle + sweep / 2) * MathF.PI / 180;
            _points.Add((new RectangleF(circle.X + diameter / 2 + MathF.Cos(middle) * diameter * .3f - 15,
                circle.Y + diameter / 2 + MathF.Sin(middle) * diameter * .3f - 15, 30, 30), i));
            angle += sweep;
        }
        if (Kind == ChartKind.Donut)
        {
            using var background = new SolidBrush(BackColor); g.FillEllipse(background, circle.X + diameter * .25f, circle.Y + diameter * .25f, diameter * .5f, diameter * .5f);
        }
    }
    private void DrawRadar(Graphics g, RectangleF plot, int[] visible)
    {
        double maximum = Math.Max(1, visible.Max(index => _series[index].Values.Max()));
        PointF center = new(plot.Left + plot.Width / 2, plot.Top + plot.Height / 2); float radius = Math.Min(plot.Width, plot.Height) * .42f;
        PointF Point(int index, double value)
        {
            double angle = -Math.PI / 2 + index * Math.PI * 2 / PointCount;
            return new PointF(center.X + (float)(Math.Cos(angle) * radius * value / maximum), center.Y + (float)(Math.Sin(angle) * radius * value / maximum));
        }
        using var grid = new Pen(Color.FromArgb(70, ForeColor));
        for (int ring = 1; ring <= 4; ring++) g.DrawPolygon(grid, Enumerable.Range(0, PointCount).Select(i => Point(i, maximum * ring / 4)).ToArray());
        for (int i = 0; i < PointCount; i++)
        {
            PointF edge = Point(i, maximum); g.DrawLine(grid, center, edge);
            TextRenderer.DrawText(g, _labels[i], Font, new Point((int)edge.X, (int)edge.Y), ForeColor);
            _points.Add((new RectangleF(edge.X - 12, edge.Y - 12, 24, 24), i));
        }
        foreach (int series in visible)
        {
            var points = Enumerable.Range(0, PointCount).Select(i => Point(i, _series[series].Values[i])).ToArray();
            using var fill = new SolidBrush(Color.FromArgb(60, SeriesColor(series))); g.FillPolygon(fill, points);
            using var outline = new Pen(SeriesColor(series), 2); g.DrawPolygon(outline, points);
        }
    }
    private void DrawRadial(Graphics g, RectangleF plot, int series)
    {
        double maximum = Math.Max(1, _series[series].Values.Max()); float diameter = Math.Min(plot.Width, plot.Height);
        for (int i = 0; i < PointCount; i++)
        {
            float inset = i * diameter / (PointCount * 2 + 1);
            var circle = new RectangleF(plot.Left + (plot.Width - diameter) / 2 + inset, plot.Top + inset, diameter - inset * 2, diameter - inset * 2);
            using var track = new Pen(Color.FromArgb(30, ForeColor), Math.Max(2, diameter / (PointCount * 3 + 2)));
            using var pen = new Pen(Palette(i), track.Width);
            g.DrawArc(track, circle, -90, 360); g.DrawArc(pen, circle, -90, (float)(_series[series].Values[i] / maximum * 360));
            _points.Add((new RectangleF(circle.Right - 20, circle.Top + circle.Height / 2 - 10, 30, 20), i));
        }
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = _points.FirstOrDefault(item => item.Bounds.Contains(e.Location));
        string text = hit.Bounds.IsEmpty ? "" : GetPointDescription(hit.Index);
        if (_tooltip.GetToolTip(this) != text) _tooltip.SetToolTip(this, text);
    }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        var hit = _legend.FirstOrDefault(item => item.Bounds.Contains(e.Location));
        if (!hit.Bounds.IsEmpty) SetSeriesVisible(hit.Index, _hidden.Contains(hit.Index));
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (PointCount == 0 || e.KeyCode is not (Keys.Left or Keys.Right)) return;
        _selectedIndex = Math.Clamp(_selectedIndex + (e.KeyCode == Keys.Right ? 1 : -1), 0, PointCount - 1);
        AccessibleDescription = GetPointDescription(_selectedIndex); _tooltip.Show(AccessibleDescription, this, 20, 30, 2000); e.Handled = true;
    }
    public override Size GetPreferredSize(Size proposedSize) => new(proposedSize.Width > 0 ? proposedSize.Width : 480, 280);
    protected override void Dispose(bool disposing) { if (disposing) _tooltip.Dispose(); base.Dispose(disposing); }
}
