using System.Runtime.CompilerServices;
using System.ComponentModel;

namespace WinformsUI;

public static class Layout
{
    /// <summary>Create and attach the root layout, selecting the initial Form client size.</summary>
    public static RootPanel Root(Form form, RootSize preset, params Control[] children)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (form.Controls.OfType<RootPanel>().Any())
            throw new InvalidOperationException("このFormにはすでにルートレイアウトがあります。");
        var root = new RootPanel(form, preset);
        root.Add(children);
        form.Controls.Add(root);
        root.ApplySize();
        return root;
    }

    public static StackPanel Stack(params Control[] children) => new StackPanel().Add(children);
    public static AutoGridPanel AutoGrid(params Control[] children) => new AutoGridPanel().Add(children);
    public static FlowLayoutPanel Row(params Control[] children) => CreateFlow(false, children);
    public static FlowLayoutPanel Wrap(params Control[] children) => CreateFlow(true, children);

    private static FlowLayoutPanel CreateFlow(bool wrap, Control[] children)
    {
        var panel = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = wrap,
            Margin = Padding.Empty, Padding = Padding.Empty, TabStop = false
        };
        foreach (var child in children)
        {
            child.Margin = new Padding(0, 0, 8, 0);
            child.TabIndex = panel.Controls.Count;
            panel.Controls.Add(child);
        }
        return panel;
    }
}

internal sealed class LayoutHints
{
    internal bool FullWidth { get; set; }
    internal bool FillHeight { get; set; }
    internal static readonly ConditionalWeakTable<Control, LayoutHints> Values = new();
    internal static LayoutHints For(Control control) => Values.GetOrCreateValue(control);
}

internal static class LayoutInvalidation
{
    internal static void NotifyAncestors(Control control)
    {
        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
            parent.PerformLayout();
    }
}

public static class LayoutExtensions
{
    /// <summary>Occupy a whole row in an AutoGrid.</summary>
    public static T FullWidth<T>(this T control) where T : Control
    {
        LayoutHints.For(control).FullWidth = true;
        LayoutInvalidation.NotifyAncestors(control);
        return control;
    }

    /// <summary>Share remaining vertical space in a Stack. MinimumSize.Height is still honored.</summary>
    public static T FillRemainingHeight<T>(this T control) where T : Control
    {
        LayoutHints.For(control).FillHeight = true;
        LayoutInvalidation.NotifyAncestors(control);
        return control;
    }
}

public abstract class ContentPanel : Panel
{
    private int _gap = 12;
    protected bool Arranging { get; set; }
    protected ContentPanel() { Margin = Padding.Empty; TabStop = false; }

    /// <summary>Spacing in logical pixels (96 DPI).</summary>
    [DefaultValue(12)]
    public int Gap
    {
        get => _gap;
        set { ArgumentOutOfRangeException.ThrowIfNegative(value); _gap = value; PerformLayout(); LayoutInvalidation.NotifyAncestors(this); }
    }

    protected int Scale(int value) => (int)Math.Round(value * DeviceDpi / 96d);
    protected Control[] VisibleChildren => Controls.Cast<Control>().Where(control => control.Visible).ToArray();

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control is not null)
        {
            e.Control.Margin = Padding.Empty;
            e.Control.TabIndex = Controls.Count - 1;
            e.Control.VisibleChanged += ChildLayoutChanged;
        }
        LayoutInvalidation.NotifyAncestors(this);
    }

    protected override void OnControlRemoved(ControlEventArgs e)
    {
        if (e.Control is not null) e.Control.VisibleChanged -= ChildLayoutChanged;
        base.OnControlRemoved(e);
        LayoutInvalidation.NotifyAncestors(this);
    }

    private void ChildLayoutChanged(object? sender, EventArgs e)
    {
        if (Arranging) return;
        PerformLayout();
        LayoutInvalidation.NotifyAncestors(this);
    }
}

/// <summary>Vertical layout; rows are inferred from its children.</summary>
public class StackPanel : ContentPanel
{
    public StackPanel Add(params Control[] children)
    {
        SuspendLayout();
        Controls.AddRange(children);
        ResumeLayout(true);
        LayoutInvalidation.NotifyAncestors(this);
        return this;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var children = VisibleChildren;
        int width = proposedSize.Width > 0 ? proposedSize.Width : Math.Max(Width, Scale(440));
        int available = Math.Max(1, width - Padding.Horizontal);
        int height = children.Sum(child => DesiredHeight(child, available))
            + Math.Max(0, children.Length - 1) * Scale(Gap) + Padding.Vertical;
        return new Size(width, height);
    }

    private int DesiredHeight(Control child, int width) => LayoutHints.For(child).FillHeight
        ? Math.Max(child.MinimumSize.Height, Scale(120))
        : Math.Max(child.MinimumSize.Height, child.GetPreferredSize(new Size(width, 0)).Height);

    protected override void OnLayout(LayoutEventArgs e)
    {
        if (Arranging) return;
        Arranging = true;
        try
        {
            base.OnLayout(e);
            var children = VisibleChildren;
            // Scrollbars change ClientSize. Measure again after arranging the current bounds,
            // so a previous wide layout cannot leave a stale horizontal scroll extent behind.
            for (int pass = 0; pass < 3; pass++)
            {
                Size viewport = ClientSize;
                int width = Math.Max(1, viewport.Width - Padding.Horizontal);
                var heights = children.Select(child => DesiredHeight(child, width)).ToArray();
                int gap = Scale(Gap);
                int baseHeight = heights.Sum() + Math.Max(0, children.Length - 1) * gap + Padding.Vertical;
                int remaining = Math.Max(0, viewport.Height - baseHeight);
                int fillCount = children.Count(child => LayoutHints.For(child).FillHeight);
                int y = Padding.Top + (AutoScroll ? AutoScrollPosition.Y : 0);
                for (int i = 0; i < children.Length; i++)
                {
                    int height = heights[i];
                    if (LayoutHints.For(children[i]).FillHeight && fillCount > 0)
                    {
                        int extra = remaining / fillCount;
                        height += extra;
                        remaining -= extra;
                        fillCount--;
                    }
                    children[i].SetBounds(Padding.Left, y, width, height);
                    y += height + gap;
                }
                if (AutoScroll)
                {
                    AutoScrollMinSize = new Size(0, baseHeight);
                    AdjustFormScrollbars(true);
                }
                if (ClientSize == viewport) break;
            }
        }
        finally { Arranging = false; }
    }
}
/// <summary>Equal-width cells; the column count is inferred from the available width.</summary>
public sealed class AutoGridPanel : ContentPanel
{
    private int _minimumFieldWidth = 220;
    [DefaultValue(220)]
    public int MinimumFieldWidth
    {
        get => _minimumFieldWidth;
        set { ArgumentOutOfRangeException.ThrowIfLessThan(value, 1); _minimumFieldWidth = value; PerformLayout(); LayoutInvalidation.NotifyAncestors(this); }
    }

    public int ColumnCount => ColumnsFor(Math.Max(1, ClientSize.Width - Padding.Horizontal));

    public AutoGridPanel Add(params Control[] children)
    {
        SuspendLayout();
        Controls.AddRange(children);
        ResumeLayout(true);
        LayoutInvalidation.NotifyAncestors(this);
        return this;
    }

    private int ColumnsFor(int width)
    {
        int count = VisibleChildren.Count(child => !LayoutHints.For(child).FullWidth);
        return Math.Max(1, Math.Min(Math.Max(1, count), (width + Scale(Gap)) / (Scale(MinimumFieldWidth) + Scale(Gap))));
    }

    private (List<(Control Control, Rectangle Bounds)> Cells, int Height) Measure(int width)
    {
        int available = Math.Max(1, width - Padding.Horizontal);
        int columns = ColumnsFor(available);
        int gap = Scale(Gap);
        int cellWidth = Math.Max(1, (available - (columns - 1) * gap) / columns);
        var cells = new List<(Control, Rectangle)>();
        var row = new List<Control>();
        int y = Padding.Top;
        bool hasRow = false;

        void FlushRow()
        {
            if (row.Count == 0) return;
            if (hasRow) y += gap;
            int rowHeight = row.Max(child => child.GetPreferredSize(new Size(cellWidth, 0)).Height);
            for (int column = 0; column < row.Count; column++)
            {
                // Distribute rounding so a completely populated row meets the right edge.
                int x = Padding.Left + column * (available + gap) / columns;
                int nextX = Padding.Left + (column + 1) * (available + gap) / columns;
                cells.Add((row[column], new Rectangle(x, y, nextX - x - gap, rowHeight)));
            }
            y += rowHeight;
            hasRow = true;
            row.Clear();
        }

        foreach (var child in VisibleChildren)
        {
            if (LayoutHints.For(child).FullWidth)
            {
                FlushRow();
                if (hasRow) y += gap;
                int height = child.GetPreferredSize(new Size(available, 0)).Height;
                cells.Add((child, new Rectangle(Padding.Left, y, available, height)));
                y += height;
                hasRow = true;
            }
            else
            {
                row.Add(child);
                if (row.Count == columns) FlushRow();
            }
        }
        FlushRow();
        return (cells, y + Padding.Bottom);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int width = proposedSize.Width > 0 ? proposedSize.Width : Math.Max(Width, Scale(MinimumFieldWidth));
        return new Size(width, Measure(width).Height);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (Arranging) return;
        Arranging = true;
        try
        {
            foreach (var (control, bounds) in Measure(ClientSize.Width).Cells) control.Bounds = bounds;
        }
        finally { Arranging = false; }
    }
}
