using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing.Imaging;

namespace WinformsUI.Sample;

internal static class ExtendedVerification
{
    internal static void Run(string outputDirectory, ICollection<string> results)
    {
        using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ClientSize = new Size(800, 700) };
        host.Show();
        void Mount(Control control)
        {
            foreach (Control previous in host.Controls.Cast<Control>().ToArray()) previous.Dispose();
            control.Dock = DockStyle.Fill; host.Controls.Add(control); Settle(host);
        }

        var accordion = new AccordionPanel();
        var first = accordion.AddSection("最初", Field.Text("名前"));
        var second = accordion.AddSection("次", Field.Text("番号"));
        Mount(accordion); first.Trigger.PerformClick(); second.Trigger.PerformClick();
        Check(!first.Expanded && second.Expanded, "Accordion enforces single expansion");
        accordion.AllowMultiple = true; first.Trigger.PerformClick(); Check(first.Expanded && second.Expanded, "Accordion permits multiple expansion");
        first.Enabled = false; bool old = first.Expanded; first.Trigger.PerformClick(); Check(first.Expanded == old, "Disabled accordion does not toggle");
        results.Add("PASS: Accordion and collapsible native trigger behavior");

        var child = new Label { Text = "内容" }; var ratio = new AspectRatioPanel(child, 16d / 9); Mount(ratio);
        Check(Math.Abs(child.Width / (double)child.Height - 16d / 9) < .02 && ratio.ClientRectangle.Contains(child.Bounds), "Aspect ratio fits the content");
        ratio.Ratio = 1; Settle(host); Check(child.Width == child.Height, "Aspect ratio can change");
        var carousel = new CarouselPanel(Ui.Label("ページ1"), Ui.Label("ページ2")); Mount(carousel);
        carousel.NextButton.PerformClick(); Check(carousel.Index == 1 && !carousel.NextButton.Enabled, "Carousel advances and stops at boundary");
        carousel.Loop = true; carousel.NavigateBy(1); Check(carousel.Index == 0, "Carousel supports looping");
        var editor = new TextBox(); var group = new InputGroup(editor, new Label { Text = "数量", AutoSize = true }, Ui.Button("実行", () => editor.Text = "10"));
        Mount(group); Check(editor.Width > 100 && editor.Left > 0, "Input group stretches its editor between addons");
        results.Add("PASS: Aspect ratio, carousel and input-group layout");

        using (var avatar = new Avatar("田"))
        {
            Check(!avatar.LoadImage(Path.Combine(outputDirectory, "missing.png")), "Avatar handles a missing image with a fallback");
            Check(avatar.Image is null && avatar.Fallback == "田", "Avatar fallback remains available");
        }
        var choices = new ChoicePicker<int>([new(1, "工程A", "加工"), new(2, "工程B", "検査"), new(3, "停止", "検査", false)], true);
        Mount(choices); choices.SelectKey(1); choices.SelectKey(2); choices.Query.Text = "検査";
        Check(choices.SelectedKeys.SequenceEqual(new[] { 1, 2 }) && choices.Options.Items.Count == 2, "Choice search preserves multi-selection");
        choices.ClearSelection(); Check(choices.SelectedKeys.Count == 0, "Choices can clear all selections");
        var slider = new MultiSlider(0, 100, 10, 80); Mount(slider); slider.SetValue(0, 90);
        Check(slider.Values.SequenceEqual(new[] { 80, 80 }), "Multiple slider values cannot cross");
        results.Add("PASS: Avatar fallback, grouped searchable multi-select and multi-value slider");

        bool executed = false;
        using (var commands = new CommandPalette([new("登録", () => executed = true, "実績", "Ctrl+S"), new("無効", () => executed = false, Enabled: false)]))
        {
            commands.Query.Text = "実績"; Check(commands.Matches.Count == 1, "Command palette searches groups and excludes disabled commands");
            Check(commands.ExecuteSelected() && executed, "Command palette executes the selected callback");
        }
        var toastHost = new ToastHost(); Mount(toastHost);
        bool action = false; var toast = toastHost.Notify("確認", durationMilliseconds: 0, action: () => action = true);
        toast.ActionButton!.PerformClick(); Check(action && toast.IsDisposed, "Toast actions execute and dismiss");
        var tracked = Complete(toastHost.TrackAsync(() => Task.CompletedTask, "処理中", "完了")); Check(tracked.Kind == ToastKind.Success, "Async toast tracks success");
        var failed = Complete(toastHost.TrackAsync(() => Task.FromException(new InvalidOperationException("失敗")), "処理中", "完了"));
        Check(failed.Kind == ToastKind.Error && failed.MessageText.Contains("失敗"), "Async toast reports errors without losing the failure message");
        var expiring = toastHost.Notify("短い通知", durationMilliseconds: 20); PumpUntil(() => expiring.IsDisposed);
        results.Add("PASS: Command execution, toast actions, expiry and async success/error states");

        string file = Path.Combine(outputDirectory, "attachment.txt"); File.WriteAllText(file, "attachment sample");
        var attachment = AttachmentView.FromFile(file); Mount(attachment);
        attachment.SetState(AttachmentState.Uploading, 65); Check(attachment.Progress == 65 && attachment.State == AttachmentState.Uploading, "Attachments expose progress");
        attachment.SetState(AttachmentState.Error, error: "再試行してください"); Check(attachment.State == AttachmentState.Error, "Attachments expose error state");
        bool removed = false; attachment.RemoveRequested += (_, _) => removed = true; attachment.RemoveButton.PerformClick(); Check(removed && attachment.IsDisposed, "Attachment removal works");
        results.Add("PASS: File attachment metadata, upload/error state and removal");

        var records = Enumerable.Range(0, 23).Select(index => new ProductionRecord { PartNumber = $"P-{index}", GoodCount = index }).ToList();
        var grid = new DataGrid<ProductionRecord>().SetData(records); var pages = new Pagination(10);
        Mount(WinformsUI.Layout.Stack(pages, grid.FillRemainingHeight()));
        using (grid.ConnectPagination(pages))
        {
            Check(grid.RowCount == 10 && pages.PageCount == 3, "Pagination limits the first page");
            pages.LastButton.PerformClick(); Check(grid.RowCount == 3 && grid.Items[0].GoodCount == 20, "Pagination navigates to the last page");
            grid.ApplyFilter(record => record.GoodCount < 5); Check(grid.RowCount == 5 && pages.Page == 0 && pages.PageCount == 1, "Filtering resets and updates pagination");
            grid.SetColumnVisible(nameof(ProductionRecord.Remarks), false); Check(!grid.Grid.Columns[nameof(ProductionRecord.Remarks)]!.Visible, "Column visibility can change");
        }
        results.Add("PASS: Data-grid pagination, filter reset and column visibility");

        var model = new ValidationModel { Name = "初期", Quantity = 2 }; var modelForm = new ModelForm<ValidationModel>(model); Mount(modelForm);
        ((TextBox)((InputField<object?>)modelForm.Fields[nameof(ValidationModel.Name)]).Editor).Text = "";
        Check(modelForm.IsDirty && !modelForm.TryRead(out _) && modelForm.Errors.Count > 0 && model.Name == "初期", "Model validation never modifies the original model");
        modelForm.Reset(); Check(!modelForm.IsDirty && modelForm.TryRead(out var read) && read!.Quantity == 2, "Model form reset and typed read work");
        ((TextBox)((InputField<object?>)modelForm.Fields[nameof(ValidationModel.Quantity)]).Editor).Text = "文字";
        Check(!modelForm.TryRead(out _), "Model form reports numeric conversion errors");
        results.Add("PASS: Model form generation, annotations, dirty state, reset and conversion errors");

        foreach (ChartKind kind in Enum.GetValues<ChartKind>())
        {
            var chart = new Chart(kind).SetData(["A", "B", "C"], new ChartSeries("実績", [10, 20, 30]), new ChartSeries("目標", [20, 25, 40])); Mount(chart);
            Capture(host, Path.Combine(outputDirectory, $"chart-{kind}.png"));
            Check(chart.GetPointDescription(1).Contains("20.00"), "Chart point descriptions expose the data");
            chart.SetSeriesVisible(1, false); Check(!chart.GetPointDescription(0).Contains("目標"), "Chart legends control series visibility");
        }
        results.Add("PASS: All seven chart modes render; tooltip data and series visibility work");

        var questionnaire = new Questionnaire([
            new("type", "種類", [new("production", "製造"), new("inspection", "検査")]),
            new("detail", "詳細", [new("A", "A"), new("B", "B")], Required: false, Multiple: true),
            new("note", "備考", AllowText: true, When: answers => answers.TryGetValue("type", out var answer) && answer.Keys.Contains("production"))]);
        Mount(questionnaire); Check(!questionnaire.Next(), "Questionnaire requires an answer");
        questionnaire.SelectAnswer("type", "production"); Check(questionnaire.Next() && questionnaire.CurrentKey == "detail", "Questionnaire moves forward");
        Check(questionnaire.Skip() && questionnaire.CurrentKey == "note", "Questionnaire supports explicit optional skips");
        questionnaire.SetTextAnswer("確認済み"); string state = Path.Combine(outputDirectory, "questions.json"); questionnaire.SaveState(state);
        questionnaire.Previous(); questionnaire.RestoreState(state); Check(questionnaire.CurrentKey == "note" && questionnaire.Answers["detail"].Skipped, "Questionnaire resumes answers, step and skip state");
        bool submitted = false; questionnaire.Submitted += (_, e) => submitted = e.Answers["note"].Text == "確認済み";
        Check(questionnaire.Submit() && submitted, "Questionnaire submits typed choices and freeform answers");
        results.Add("PASS: Questionnaire required choices, conditional steps, skip, freeform, resume and submit");

        var transcript = new MessageScroller(); Mount(transcript);
        for (int i = 0; i < 15; i++) transcript.Append(new ConversationMessage($"m{i}", "作業者", string.Join("\n", Enumerable.Repeat($"メッセージ{i}", 4))));
        transcript.JumpTo("m7"); var before = transcript.CapturePosition();
        transcript.PrependHistory([new ConversationMessage("history", "履歴", "以前のメッセージ")]);
        var after = transcript.CapturePosition(); Check(before.MessageId == after.MessageId && Math.Abs(before.Offset - after.Offset) <= 2, "Prepended history preserves the reader's anchor");
        transcript.UpdateMessage("m14", message => message.Bubble.SetText("更新された内容"));
        Check(transcript.CapturePosition().MessageId == before.MessageId && !transcript.FollowingLiveEdge, "Streaming away from the live edge preserves position");
        transcript.FollowLatest(); Check(transcript.FollowingLiveEdge, "Reader can explicitly follow latest"); transcript.PauseFollowing();
        Check(!transcript.FollowingLiveEdge, "Reader can pause following");
        var bubble = transcript.Messages[0].Bubble;
        var bodyBox = bubble.Controls.OfType<TextBox>().Single();
        Check(bubble.Controls.Cast<Control>().Where(control => control != bodyBox && control.Visible).All(control => !control.Bounds.IntersectsWith(bodyBox.Bounds)),
            "Empty auxiliary panels do not cover message text");
        Capture(host, Path.Combine(outputDirectory, "transcript.png"));
        results.Add("PASS: Transcript history anchoring, streamed updates and explicit follow/pause");
        transcript.UpdateMessage("m1", message => { message.Bubble.MaximumLines = 100; message.Bubble.SetText(new string('あ', 150)); });
        var resizeAnchor = transcript.CapturePosition(); host.ClientSize = new Size(500, 700); Settle(host);
        Check(transcript.CapturePosition().MessageId == resizeAnchor.MessageId && Math.Abs(transcript.CapturePosition().Offset - resizeAnchor.Offset) <= 2,
            "Reflow above the reader preserves the message and offset");
        host.ClientSize = new Size(800, 700); Settle(host);
        transcript.Virtualize = true; transcript.JumpTo("m7"); Settle(host);
        Check(transcript.RealizedMessageCount < transcript.Messages.Count, "Transcript virtualizes rows outside the viewport");
        var virtualPosition = transcript.CapturePosition(); transcript.PrependHistory([new ConversationMessage("virtual-history", "履歴", "追加履歴")]);
        Check(transcript.CapturePosition().MessageId == virtualPosition.MessageId, "Virtual history preserves the reader's message anchor");
        transcript.JumpTo("m14"); Check(transcript.Messages.Single(message => message.Id == "m14").Parent is not null, "Jumping realizes the target message");
        results.Add("PASS: Virtual transcript rows, history preservation and jump realization");

        var breadcrumb = new Breadcrumb(new("root", "ルート", () => executed = true), new("a", "A"), new("b", "B"), new("c", "C"), new("end", "現在")) { MaxVisible = 3 };
        Mount(breadcrumb); Check(breadcrumb.Path.Count == 5 && breadcrumb.Controls[0].Controls.OfType<LinkLabel>().Any(link => link.Text == "…"), "Breadcrumb collapses long paths into an overflow menu");
        var link = breadcrumb.Controls[0].Controls.OfType<LinkLabel>().First(); executed = false;
        typeof(LinkLabel).GetMethod("OnLinkClicked", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(link, [new LinkLabelLinkClickedEventArgs(link.Links[0])]);
        Check(executed, "Breadcrumb link actions execute");
        using (var sheet = new SheetForm(host, Field.Text("詳細"), SheetSide.Right, 250))
        {
            sheet.Show(host); Application.DoEvents(); Check(sheet.Right == host.RectangleToScreen(host.ClientRectangle).Right, "Sheet aligns with the owner's right edge");
            sheet.SnapTo(300); Check(sheet.Width == 300, "Sheet snap size changes"); sheet.Close();
        }
        using (var drawer = new DrawerForm(host, Ui.Label("下部"))) { drawer.SetSnapPoints(180, 300); drawer.Show(host); Application.DoEvents(); drawer.SnapTo(180); Check(drawer.Height == 180, "Drawer supports snap sizes"); drawer.Close(); }
        results.Add("PASS: Breadcrumb overflow/link callbacks, side sheet and drawer positioning/snap sizes");

        Check(NaturalDates.TryParse("来週月曜", out var relativeDate, new DateTime(2026, 10, 8)) && relativeDate == new DateTime(2026, 10, 12), "Natural dates resolve Japanese weekdays");
        Check(NaturalDates.TryParse("3 days ago", out var pastDate, new DateTime(2026, 10, 8)) && pastDate == new DateTime(2026, 10, 5), "Natural dates resolve relative days");
        using (var dateText = Field.DateText("日付"))
        {
            ((TextBox)dateText.Editor).Text = "不明な日付"; Check(!dateText.ValidateValue(), "Natural date fields reject invalid text");
            ((TextBox)dateText.Editor).Text = "明日"; Check(dateText.ValidateValue() && dateText.Value == DateTime.Today.AddDays(1), "Natural date fields return typed dates");
        }
        results.Add("PASS: Natural date parsing, typed dates and invalid-input validation");

        var field = Field.Text("テーマ入力").WithHelp("説明"); var button = Ui.Button("操作", () => { }); var container = WinformsUI.Layout.Stack(field, button); Mount(container);
        Color original = field.Editor.BackColor; Font originalFont = field.Font;
        using (var theme = UiTheme.Attach(host))
        {
            var template = new ThemeTemplate { Name = "検証", Background = "#202020", Foreground = "#EEEEEE", FontSize = 12, Gap = 20,
                Roles = new() { ["input"] = new() { Background = "#303030" } } };
            string themeFile = Path.Combine(outputDirectory, "template.json"); template.Save(themeFile); theme.Use(ThemeTemplate.Load(themeFile));
            Check(field.Editor.BackColor == Color.FromArgb(48, 48, 48) && container.Gap == 20, "Theme templates apply roles and spacing");
            var added = Ui.Button("追加", () => { }); container.Add(added); Check(added.BackColor == Color.FromArgb(32, 32, 32), "Dynamically added controls inherit the current template");
            var addedField = Field.Text("追加入力"); container.Add(addedField);
            using var themedChild = new Form(); using var childTheme = UiTheme.Inherit(themedChild, host);
            Check(childTheme is not null && themedChild.BackColor == host.BackColor, "Owned surfaces inherit the current template");
            var trigger = Ui.Button("ポップアップ", () => { }); container.Add(trigger); Settle(host);
            using (var popup = new SurfacePopup(Ui.Label("詳細"), new Size(180, 70)))
            {
                popup.Show(trigger); Check(popup.IsOpen && popup.Content.BackColor == host.BackColor, "Rich popovers inherit the owner theme");
                popup.Close(); Check(!popup.IsOpen, "Rich popovers can close");
            }
            using (var hover = new SurfacePopup(Ui.Label("ホバー内容"), new Size(180, 70)))
            {
                hover.AttachHover(trigger, 20, 100);
                host.Activate(); trigger.Focus();
                Check(trigger.Focused, "Hover trigger can receive native keyboard focus");
                PumpUntil(() => hover.IsOpen);
                var hoverWait = System.Diagnostics.Stopwatch.StartNew();
                while (hoverWait.ElapsedMilliseconds < 150) { Application.DoEvents(); Thread.Sleep(5); }
                Check(hover.IsOpen, "Keyboard-triggered hover cards stay open for interaction");
                hover.Close();
            }
            theme.Use(ThemeTemplate.Native); Check(field.Editor.BackColor == original && container.Gap == 12 && Math.Abs(field.Font.SizeInPoints - originalFont.SizeInPoints) < .01, "Native theme restores editor, spacing and fonts");
            Check(added.BackColor == host.BackColor, "Dynamic controls also restore native background");
            Check(themedChild.BackColor == host.BackColor && childTheme!.Template.Name == "Native", "Owned surfaces follow later template switches");
            var addedLabel = addedField.Controls.OfType<Label>().First();
            Check(Math.Abs(addedLabel.Font.SizeInPoints - Math.Max(8, field.Font.SizeInPoints - 1)) < .01, "Dynamically added field labels restore native small fonts");
        }
        results.Add("PASS: JSON theme roundtrip, global role/font/spacing changes, dynamic controls and native restoration");
        using (var gallery = new ComponentGalleryForm { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000) })
        {
            gallery.Show();
            for (int index = 0; index < gallery.Tabs.TabCount; index++)
            {
                gallery.Tabs.SelectedIndex = index; Settle(gallery);
                Capture(gallery, Path.Combine(outputDirectory, $"gallery-{index}.png"));
            }
            gallery.ThemeSelector.SelectedIndex = 1; Settle(gallery); Check(gallery.BackColor == Color.FromArgb(246, 251, 244), "Gallery loads Normal from packaged JSON");
            Capture(gallery, Path.Combine(outputDirectory, "gallery-normal.png"));
            gallery.ThemeSelector.SelectedIndex = 2; Settle(gallery); Check(gallery.BackColor == Color.FromArgb(32, 32, 32), "Gallery loads Dark from packaged JSON");
            Capture(gallery, Path.Combine(outputDirectory, "gallery-dark.png"));
            gallery.ThemeSelector.SelectedIndex = 0; Settle(gallery); gallery.Close();
        }
        results.Add("PASS: All eight sample gallery tabs render and switch Native, Normal and Dark themes");
        GanttAndThemeVerification.Run(outputDirectory, results);
        host.Close();
    }

    private static void Settle(Form form) { for (int i = 0; i < 3; i++) { form.PerformLayout(); Application.DoEvents(); } }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Capture(Form form, string path) { using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(path, ImageFormat.Png); }
    private static void PumpUntil(Func<bool> completed)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!completed()) { Application.DoEvents(); if (timer.ElapsedMilliseconds > 3000) throw new TimeoutException("UI操作が完了しませんでした。"); Thread.Sleep(5); }
    }
    private static T Complete<T>(Task<T> task) { PumpUntil(() => task.IsCompleted); return task.GetAwaiter().GetResult(); }
    private sealed class ValidationModel
    {
        [DisplayName("名前"), Required]
        public string Name { get; set; } = "";
        [DisplayName("数量"), Range(0, 100)]
        public int Quantity { get; set; }
    }
}
