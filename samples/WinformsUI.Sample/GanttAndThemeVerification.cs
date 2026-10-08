using System.Drawing.Imaging;
using System.Reflection;

namespace WinformsUI.Sample;

internal static class GanttAndThemeVerification
{
    internal static void Run(string output, ICollection<string> results)
    {
        using var host = new Form { ClientSize = new Size(900, 480), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000) };
        var button = Ui.Button("角丸", () => { }); button.Size = new Size(160, 50);
        var square = Ui.Button("角なし", () => { }).WithRadius(0);
        var field = Field.Text("入力");
        var panel = WinformsUI.Layout.Stack(button, square, field); panel.Dock = DockStyle.Fill;
        host.Controls.Add(panel); host.Show(); Settle(host);
        using var theme = UiTheme.Attach(host);
        var normal = ThemeTemplate.Load(Path.Combine(AppContext.BaseDirectory, "themes", "normal.json"));
        theme.Use(normal); Settle(host);
        Check(button.BackColor == Color.FromArgb(46, 125, 50) && field.Editor.BackColor.ToArgb() == Color.White.ToArgb(), $"Normal uses green buttons and white inputs: button={button.BackColor}, input={field.Editor.BackColor}");
        Check(button.Region is not null && !button.Region.IsVisible(0, 0) && button.Region.IsVisible(button.Width / 2, button.Height / 2), "Theme rounds button corners without cutting its center");
        Check(square.Region is null, "Explicit zero radius overrides theme");
        button.WithRadius(18); button.Width += 40; Settle(host);
        Check(button.Region is not null && !button.Region.IsVisible(button.Width - 1, 0), "Individual rounding follows resize");
        theme.Use(ThemeTemplate.Native);
        Check(button.Region is not null && field.Editor.Region is null, "Native restores themed rounding while retaining individual overrides");
        button.ResetRadius(); Check(button.Region is null, "ResetRadius restores original region");
        theme.Use(normal with { Roles = new() { ["custom"] = new() { CornerRadius = 20 } } });
        square.ResetRadius(); square.WithRole("custom"); Check(square.Region is not null, "Role-specific radius can be applied after theme attachment");
        using (var originalRegion = new Region(new Rectangle(4, 4, 80, 30)))
        using (var custom = new Button { Region = originalRegion.Clone() })
        {
            custom.WithRadius(10); custom.ResetRadius();
            Check(custom.Region is not null && !custom.Region.IsVisible(0, 0) && custom.Region.IsVisible(10, 10), "Rounding restores an existing custom region");
        }
        Reject<InvalidDataException>(() => theme.Use(normal with { CornerRadius = -1 }));
        Reject<ArgumentOutOfRangeException>(() => button.WithRadius(-1));
        results.Add("PASS: Normal palette, theme/role/individual radii, resize, reset, native/custom-region restoration and validation");

        theme.Use(ThemeTemplate.Native); host.Controls.Remove(panel); panel.Dispose();
        var source = Enumerable.Range(0, 35).Select(index => new GanttTask { Id = $"task-{index}", Title = $"工程 {index + 1}", Resource = "ラインA", Start = DateTime.Today.AddDays(index), End = DateTime.Today.AddDays(index + 3), Progress = index % 11 * 10 }).ToList();
        var gantt = new GanttChart { Dock = DockStyle.Fill }.SetData(source);
        host.Controls.Add(gantt); Settle(host);
        Check(gantt.Items.Count == 35 && ReferenceEquals(gantt.Items[0], source[0]), "Gantt keeps list model references");
        Rectangle bounds = gantt.GetTaskBounds("task-0");
        int dayWidth = (int)Math.Round(32 * gantt.DeviceDpi / 96d);
        int totalInset = (int)Math.Round(4 * gantt.DeviceDpi / 96d);
        Check(bounds.Width == dayWidth * 4 - totalInset, $"Gantt includes both start and end dates at current DPI: width={bounds.Width}, dpi={gantt.DeviceDpi}");
        Point center = new(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        Check(ReferenceEquals(gantt.HitTest(center), source[0]) && gantt.HitTest(new Point(center.X, 10)) is null, "Gantt hit testing excludes the header");
        int events = 0; gantt.SelectionChanged += (_, _) => events++;
        typeof(GanttChart).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(gantt, [new MouseEventArgs(MouseButtons.Left, 1, center.X, center.Y, 0)]);
        Check(ReferenceEquals(gantt.SelectedTask, source[0]) && events == 1, "Click selects the original model and raises an event");
        typeof(GanttChart).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(gantt, [new KeyEventArgs(Keys.Down)]);
        Check(ReferenceEquals(gantt.SelectedTask, source[1]) && events == 2, "Down arrow selects next row");
        gantt.SelectTask("task-34"); Settle(host);
        Check(gantt.AutoScrollPosition.Y < 0 && gantt.GetTaskBounds("task-34").Bottom <= gantt.ClientSize.Height, "Selection scrolls the row into view");
        gantt.ScrollToDate(DateTime.Today.AddDays(25)); Settle(host);
        Check(gantt.AutoScrollPosition.X < 0, "Timeline scrolls horizontally");
        bounds = gantt.GetTaskBounds("task-34");
        center = new(bounds.Left + 5, bounds.Top + 5);
        Check(ReferenceEquals(gantt.HitTest(center), source[34]), "Hit testing follows both scroll offsets");
        source.RemoveAt(34); gantt.RefreshData(); Check(gantt.SelectedTask is null, "Refreshing removes stale selections");
        source[0].Progress = 75; gantt.RefreshData(); Check(gantt.Items[0].Progress == 75, "List changes update the view");
        foreach (var template in new[] { ThemeTemplate.Native, normal, ThemeTemplate.Load(Path.Combine(AppContext.BaseDirectory, "themes", "dark.json")) })
        {
            theme.Use(template);
            foreach (var scale in Enum.GetValues<GanttScale>())
            {
                gantt.TimelineScale = scale; gantt.FitToTasks(); Settle(host);
                using var bitmap = new Bitmap(host.Width, host.Height); host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(output, $"gantt-{template.Name.ToLowerInvariant()}-{scale.ToString().ToLowerInvariant()}.png"), ImageFormat.Png);
            }
        }
        Reject<ArgumentException>(() => gantt.SetData([new() { Id = "duplicate" }, new() { Id = "duplicate" }]));
        Reject<ArgumentException>(() => gantt.SetData([new() { Id = "bad", Progress = 101 }]));
        Reject<ArgumentException>(() => gantt.SetData([new() { Id = "bad", Start = DateTime.Today.AddDays(1), End = DateTime.Today }]));
        Reject<ArgumentException>(() => gantt.SetViewRange(DateTime.Today, DateTime.Today.AddDays(-1)));
        Check(gantt.Items.Count == source.Count, "Invalid replacements leave existing data intact");
        gantt.SetData([]); Settle(host); Check(gantt.Items.Count == 0 && gantt.SelectedTask is null, "Empty schedules render and clear selection");
        results.Add("PASS: Gantt typed list, inclusive dates, click/keyboard selection, two-axis scrolling, hit tests, list updates, empty/invalid data and nine theme/scale renders");
        host.Close();
    }
    private static void Settle(Form form) { for (int i = 0; i < 3; i++) { form.PerformLayout(); Application.DoEvents(); } }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}"); }
}
