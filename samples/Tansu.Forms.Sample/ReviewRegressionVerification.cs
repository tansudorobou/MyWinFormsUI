using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Tansu.Forms.Sample;

internal static class ReviewRegressionVerification
{
    internal static void Run(ICollection<string> results)
    {
        VerifyModelCopy();
        results.Add("PASS: Model reads preserve hidden identity, read-only state and loaded snapshots without changing the source");
        VerifyMinimumOverride();
        results.Add("PASS: Later application minimums survive preset, shown/DPI callbacks and root disposal; automatic sizing can be re-enabled");
        VerifyThemeMoves();
        results.Add("PASS: Removed control trees restore native styles and follow only their current theme after reparenting");
        using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
            Location = new Point(-10000, -10000), ClientSize = new Size(800, 600) };
        host.Show();
        void Mount(Control control)
        {
            foreach (Control previous in host.Controls.Cast<Control>().ToArray()) previous.Dispose();
            control.Dock = DockStyle.Fill; host.Controls.Add(control); host.PerformLayout(); Application.DoEvents();
        }
        VerifyChart(Mount);
        results.Add("PASS: Chart legend clicks restore the last hidden series; native arrow-key preprocessing and dispatch change the selected point");
        VerifyCarousel(Mount);
        results.Add("PASS: Carousel loop changes immediately update boundary buttons, preserve the index and handle empty carousels");
        VerifyQuestions(Mount);
        results.Add("PASS: Radio, checkbox and text answers refresh conditional question progress/navigation while retaining focus and editor controls");
        host.Close();
    }

    private static void VerifyModelCopy()
    {
        var source = new CopyModel { Id = 42, Name = "original" };
        using var form = new ModelForm<CopyModel>(source);
        var name = (InputField<object?>)form.Fields[nameof(CopyModel.Name)];
        name.Value = "edited";
        Check(form.TryRead(out var read) && read!.Id == 42 && read.Token == source.Token && read.Locked == source.Locked,
            "Hidden identity and read-only properties must survive a model read");
        Check(read!.Name == "edited" && source.Name == "original" && !ReferenceEquals(read, source), "Only the returned copy changes");
        read.Id = 999; source.Id = 100;
        Check(form.TryRead(out var again) && again!.Id == 42, "Reads must use the loaded snapshot, not later mutations");
        form.Reset(); Check(!form.IsDirty && name.Value as string == "original", "Reset restores loaded input values");
        var replacement = new CopyModel { Id = 7, Name = "replacement" };
        form.LoadModel(replacement);
        Check(form.TryRead(out var replaced) && replaced!.Id == 7 && replaced.Token == replacement.Token, "LoadModel replaces the complete snapshot");
    }

    private static void VerifyMinimumOverride()
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000) };
        var root = Tansu.Forms.Layout.Layout.Root(form, RootSize.Standard);
        var requested = new Size(480, 400);
        form.MinimumSize = requested;
        form.Show();
        var expected = form.MinimumSize;
        root.UseSize(RootSize.Portrait);
        Check(form.MinimumSize == expected && !root.AutoMinimumSize, "A later application minimum must survive showing and preset changes");
        root.MinimumClientSize = new Size(300, 200);
        typeof(RootPanel).GetMethod("OnDpiChangedAfterParent", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(root, [EventArgs.Empty]);
        Check(form.MinimumSize == expected, "DPI/minimum-client callbacks must preserve application ownership");
        root.AutoMinimumSize = true;
        Check(root.AutoMinimumSize && form.MinimumSize != expected, "An explicit opt-in resumes automatic sizing");
        form.MinimumSize = requested;
        root.UseSize(RootSize.Standard);
        Check(form.MinimumSize == requested && !root.AutoMinimumSize, "Overrides after the form is shown must also be preserved");
        root.Dispose(); Check(form.MinimumSize == requested, "Disposing the root must not revert an application minimum");
        form.Close();
    }

    private static void VerifyThemeMoves()
    {
        using var first = new Form(); using var second = new Form();
        var tree = new Panel(); var button = new Button(); tree.Controls.Add(button); first.Controls.Add(tree);
        Color native = button.BackColor; float nativeFont = button.Font.SizeInPoints;
        using var firstTheme = UiTheme.Attach(first, new ThemeTemplate { Name = "first", Background = "#112233", FontSize = 14 });
        using var secondTheme = UiTheme.Attach(second, new ThemeTemplate { Name = "second", Background = "#445566", FontSize = 12 });
        second.Controls.Add(tree);
        Check(button.BackColor == Color.FromArgb(68, 85, 102) && button.Font.SizeInPoints == 12, "The whole moved tree adopts its new theme");
        firstTheme.Use(new ThemeTemplate { Name = "changed", Background = "#778899", FontSize = 18 });
        firstTheme.Dispose();
        Check(button.BackColor == Color.FromArgb(68, 85, 102) && button.Font.SizeInPoints == 12, "Former owners cannot change or restore moved controls");
        second.Controls.Remove(tree);
        Check(button.BackColor == native && Math.Abs(button.Font.SizeInPoints - nativeFont) < .01, "Detached controls restore their native style");
        secondTheme.Use(ThemeTemplate.Native);
        var added = new Button(); tree.Controls.Add(added);
        Check(added.BackColor != Color.FromArgb(68, 85, 102), "Detached trees must not retain theme event subscriptions");
        secondTheme.Use(new ThemeTemplate { Name = "new", Background = "#AABBCC" });
        second.Controls.Add(tree);
        Check(added.BackColor == Color.FromArgb(170, 187, 204), "Reattached trees include descendants added while detached");
        secondTheme.Use(ThemeTemplate.Native);
        Check(button.BackColor == native, "Native restoration remains correct after reattachment");
    }

    private static void VerifyChart(Action<Control> mount)
    {
        var chart = new Chart().SetData(["A", "B"], new ChartSeries("series", [1, 2])); mount(chart);
        using var bitmap = new Bitmap(chart.Width, chart.Height);
        void Paint() => chart.DrawToBitmap(bitmap, chart.ClientRectangle);
        void ClickLegend() => typeof(Chart).GetMethod("OnMouseClick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(chart, [new MouseEventArgs(MouseButtons.Left, 1, 10, 15, 0)]);
        Paint(); ClickLegend(); Paint();
        Check(!chart.GetPointDescription(0).Contains("series"), "The legend click hides the only series");
        ClickLegend(); Paint();
        Check(chart.GetPointDescription(0).Contains("series"), "The visible legend restores the last hidden series");
        Check(chart.Focus(), "The chart receives native focus");
        void Press(Keys key)
        {
            var message = Message.Create(chart.Handle, 0x0100, (nint)key, 0);
            Check(chart.PreProcessControlMessage(ref message) == PreProcessControlState.MessageNeeded,
                "Arrow keys must reach the focused chart instead of dialog navigation");
            SendMessage(chart.Handle, 0x0100, (nint)key, 0);
        }
        Press(Keys.Right); Check(chart.AccessibleDescription == chart.GetPointDescription(1), "Right selects the next point");
        Press(Keys.Left); Check(chart.AccessibleDescription == chart.GetPointDescription(0), "Left selects the preceding point");
    }

    private static void VerifyCarousel(Action<Control> mount)
    {
        var carousel = new CarouselPanel(new Label(), new Label()) { Loop = true }; mount(carousel);
        int changes = 0; carousel.IndexChanged += (_, _) => changes++;
        Check(carousel.PreviousButton.Enabled, "Loop initialization enables Previous at the first page");
        carousel.PreviousButton.PerformClick(); Check(carousel.Index == 1, "Previous loops to the final page");
        carousel.Loop = false;
        Check(!carousel.NextButton.Enabled && carousel.Index == 1 && changes == 1, "Changing loop mode only changes button availability");
        carousel.Loop = true; carousel.NextButton.PerformClick(); Check(carousel.Index == 0, "Next loops after runtime re-enabling");
        using var empty = new CarouselPanel { Loop = true };
        Check(!empty.PreviousButton.Enabled && !empty.NextButton.Enabled, "An empty looping carousel has no enabled navigation");
    }

    private static void VerifyQuestions(Action<Control> mount)
    {
        Questionnaire Create(bool multiple = false) => new([
            new("type", "type", [new("yes", "Yes"), new("no", "No")], Multiple: multiple),
            new("detail", "detail", AllowText: true, When: answers => answers.TryGetValue("type", out var answer) && answer.Keys.Contains("yes"))
        ]);
        var questions = Create(); mount(questions);
        var yes = Descendants(questions).OfType<RadioButton>().First();
        var no = Descendants(questions).OfType<RadioButton>().Last();
        yes.Focus(); yes.Checked = true;
        Check(questions.NextButton.Visible && !questions.SubmitButton.Visible && yes.Focused && !yes.IsDisposed,
            "A native radio selection reveals Next without recreating or defocusing the editor");
        Check(Descendants(questions).OfType<Label>().Any(label => label.Text == "質問 1 / 2"), "Progress reflects the newly visible question");
        no.Checked = true;
        Check(!questions.NextButton.Visible && questions.SubmitButton.Visible, "Changing the answer removes the conditional step immediately");
        yes.Checked = true; questions.NextButton.PerformClick(); Check(questions.CurrentKey == "detail", "The revealed Next button navigates to detail");
        questions.PreviousButton.PerformClick(); Check(questions.CurrentKey == "type", "Previous returns to the condition source");
        var multiple = Create(true); mount(multiple);
        var check = Descendants(multiple).OfType<CheckBox>().First(); check.Checked = true;
        Check(multiple.NextButton.Visible, "Checkbox answers update conditional navigation");
        check.Checked = false; Check(multiple.SubmitButton.Visible, "Unchecking removes the conditional question");
        var textQuestions = new Questionnaire([
            new("text", "text", AllowText: true),
            new("detail", "detail", AllowText: true, When: answers => answers.TryGetValue("text", out var answer) && answer.Text.Length > 0)
        ]); mount(textQuestions);
        var text = Descendants(textQuestions).OfType<TextBox>().Single(); text.Focus(); text.Text = "show detail";
        Check(textQuestions.NextButton.Visible && text.Focused, "Text answers update navigation without losing focus");
        text.Text = ""; Check(textQuestions.SubmitButton.Visible && !textQuestions.NextButton.Visible, "Clearing text removes the conditional step");
    }

    private static IEnumerable<Control> Descendants(Control control) => control.Controls.Cast<Control>()
        .SelectMany(child => new[] { child }.Concat(Descendants(child)));
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);
    private sealed class CopyModel
    {
        [Browsable(false)] public int Id { get; set; }
        public string Name { get; set; } = "";
        public Guid Token { get; } = Guid.NewGuid();
        [ReadOnly(true)] public string Locked { get; set; } = "read-only";
    }
}
