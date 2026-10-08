using UiLayout = WinformsUI.Layout;

namespace WinformsUI.Sample;

/// <summary>Code-only examples of custom behaviors and native alternatives. No external services.</summary>
public sealed class ComponentGalleryForm : Form
{
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 24, Text = "標準コントロールを使った部品一覧" };
    private readonly List<IDisposable> _resources = [];
    private readonly ThemeSession _theme;
    private int _messageId;
    public ComboBox ThemeSelector { get; } = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    public TabControl Tabs => _tabs;

    public ComponentGalleryForm()
    {
        Text = "WinformsUI — コンポーネント一覧"; ClientSize = new Size(1200, 800); MinimumSize = new Size(640, 480);
        StartPosition = FormStartPosition.CenterParent;
        ThemeSelector.Items.AddRange(["Native", "Normal（緑）", "Dark", "JSONから読み込む…"]); ThemeSelector.SelectedIndex = 0;
        var themes = UiLayout.Row(new Label { Text = "テーマ", AutoSize = true }, ThemeSelector); themes.Dock = DockStyle.Top;
        Controls.AddRange([_tabs, _status, themes]);
        BuildComposition(); BuildInputs(); BuildNotifications(); BuildCharts(); BuildQuestions(); BuildMessages(); BuildNative(); BuildGantt();
        _theme = UiTheme.Attach(this);
        ThemeSelector.SelectedIndexChanged += (_, _) =>
        {
            try
            {
                if (ThemeSelector.SelectedIndex == 0) _theme.Use(ThemeTemplate.Native);
                else if (ThemeSelector.SelectedIndex is 1 or 2) _theme.Use(ThemeTemplate.Load(Path.Combine(AppContext.BaseDirectory, "themes", ThemeSelector.SelectedIndex == 1 ? "normal.json" : "dark.json")));
                else
                {
                    using var picker = new OpenFileDialog { Filter = "テーマJSON|*.json" };
                    if (picker.ShowDialog(this) == DialogResult.OK) _theme.Use(ThemeTemplate.Load(picker.FileName));
                }
                _status.Text = $"テーマ: {_theme.Template.Name}";
            }
            catch (Exception error) when (error is IOException or System.Text.Json.JsonException or InvalidDataException) { _status.Text = error.Message; }
        };
    }
    private StackPanel Page(string title)
    {
        var page = new TabPage(title); _tabs.TabPages.Add(page);
        var body = UiLayout.Stack(); body.Dock = DockStyle.Fill; body.Padding = new Padding(12); body.AutoScroll = true;
        page.Controls.Add(body); return body;
    }
    private void BuildComposition()
    {
        var body = Page("構成");
        var path = new Breadcrumb(new("home", "ホーム"), new("factory", "工場"), new("line", "ライン"), new("records", "実績"));
        path.NavigationRequested += (_, e) => _status.Text = $"移動先: {e.Key}";
        var accordion = new AccordionPanel(); accordion.AddSection("入力補助", Field.Text("作業者").WithHelp("ラベルと入力欄は一つの部品です。"));
        accordion.AddSection("詳細", Ui.Label("一つだけ展開するモード。AllowMultipleで複数展開できます。"));
        var carousel = new CarouselPanel(Ui.Label("最初のページ"), Ui.Label("次のページ"), Ui.Label("最後のページ")) { Loop = true };
        var ratio = new AspectRatioPanel(new Label { Text = "16:9の内容領域", TextAlign = ContentAlignment.MiddleCenter, BorderStyle = BorderStyle.FixedSingle }) { Height = 180 };
        var hoverButton = Ui.Button("ホバーで詳細", () => _status.Text = "詳細はホバーまたはキーボードフォーカスでも表示できます。");
        var hover = new SurfacePopup(UiLayout.Stack(Ui.Label("ラインA"), Ui.Label("担当: 田中"), Ui.Button("担当を確認", () => _status.Text = "担当を確認しました。")), new Size(280, 150));
        hover.AttachHover(hoverButton); _resources.Add(hover);
        body.Add(path, accordion, carousel, ratio, UiLayout.Wrap(hoverButton,
            Ui.Button("シート", () => new SheetForm(this, Field.Text("シート内の入力")).Show(this)),
            Ui.Button("ドロワー", () => { var drawer = new DrawerForm(this, Field.Text("下部ドロワー")); drawer.SetSnapPoints(180, 320, 500); drawer.Show(this); }),
            Ui.Button("コマンド", () => new CommandPalette([new("実績を開く", () => new SampleForm().Show(this), "画面", "Ctrl+R"), new("入力へ", () => _tabs.SelectedIndex = 1, "移動")]).Show(this))));
        var rounded = Ui.Button("角丸の例", () => _status.Text = "WithRadiusで個別に丸みを指定できます。").WithRadius(12);
        var radius = Field.Number("角の丸み", min: 0, max: 32, initialValue: 12);
        radius.ValueChanged += (_, _) => rounded.WithRadius((int)radius.Value);
        body.Add(UiLayout.Row(radius, rounded, Ui.Button("角なしの例", () => { }).WithRadius(0)));
    }
    private void BuildInputs()
    {
        var body = Page("入力・表");
        var editor = new ModelForm<ProductionRecord>(new ProductionRecord { WorkDate = DateTime.Today, PartNumber = "P-102", Line = "A", GoodCount = 100 });
        var records = SampleData.CreateRecords(); var grid = new DataGrid<ProductionRecord>().SetData(records);
        var search = new SearchPanel().AddText(nameof(ProductionRecord.PartNumber), "品番")
            .AddSelect(nameof(ProductionRecord.Line), "ライン", SampleData.LineChoices).Connect(grid);
        var pages = new Pagination(5); _resources.Add(grid.ConnectPagination(pages));
        var group = new InputGroup(new TextBox(), new Label { Text = "品番", AutoSize = true }, Ui.Button("確認", () => _status.Text = "入力グループのボタンを押しました。"));
        var picker = new ChoicePicker<string>([new("A", "ラインA", "加工"), new("B", "ラインB", "加工"), new("C", "ラインC", "検査")], true);
        body.Add(editor, UiLayout.Wrap(Ui.Button("モデルを登録", () =>
        {
            if (editor.TryRead(out var model)) { records.Add(model!); grid.RefreshData(); _status.Text = "モデルを登録しました。"; }
            else _status.Text = string.Join(" / ", editor.Errors);
        }), Ui.Button("リセット", editor.Reset), Ui.Button("備考列を切替", () => grid.SetColumnVisible(nameof(ProductionRecord.Remarks), !grid.Grid.Columns[nameof(ProductionRecord.Remarks)]!.Visible))),
            group, Field.DateText("日付を文字で入力", DateTime.Today), picker, new MultiSlider(0, 100, 20, 80), search, pages, grid.FillRemainingHeight());
    }
    private void BuildNotifications()
    {
        var body = Page("通知・添付"); var notifications = new ToastHost(); var attachments = new AttachmentList();
        body.Add(UiLayout.Wrap(
            Ui.Button("通知", () => notifications.Notify("実績を登録しました。", ToastKind.Success)),
            Ui.Button("操作付き通知", () => notifications.Notify("再確認が必要です。", ToastKind.Warning, action: () => _status.Text = "確認しました。")),
            Ui.Button("非同期処理", async () => await notifications.TrackAsync(() => Task.Delay(800), "処理中", "処理が完了しました。"))), notifications, attachments);
        var status = new AttachmentView("sample.csv", 1024); status.SetState(AttachmentState.Uploading, 60);
        body.Add(status, UiLayout.Row(Ui.Button("添付を完了", () => status.SetState(AttachmentState.Done)), Ui.Button("添付をエラー", () => status.SetState(AttachmentState.Error, error: "再試行できます。"))));
    }
    private void BuildCharts()
    {
        var body = Page("チャート"); var chart = new Chart();
        chart.SetData(Enumerable.Range(0, 6).Select(index => DateTime.Today.AddDays(index - 5).ToString("MM/dd")),
            new ChartSeries("良品数", [120, 90, 140, 110, 160, 130]), new ChartSeries("目標", [100, 100, 120, 120, 140, 140]));
        var type = Field.Select("グラフ種類", Enum.GetNames<ChartKind>());
        type.ValueChanged += (_, _) => chart.Kind = Enum.Parse<ChartKind>(type.Value);
        var stack = new CheckBox { Text = "積み上げ", AutoSize = true }; stack.CheckedChanged += (_, _) => { chart.Stacked = stack.Checked; chart.Invalidate(); };
        body.Add(type, stack, Ui.Label("凡例をクリックすると系列の表示を切り替えられます。左右キーで値を読み取れます。"), chart.FillRemainingHeight());
    }
    private void BuildQuestions()
    {
        var body = Page("質問");
        var questionnaire = new Questionnaire([
            new("work", "作業の種類", [new("production", "製造"), new("inspection", "検査")]),
            new("checks", "確認項目", [new("quality", "品質確認"), new("equipment", "設備確認")], Required: false, Multiple: true),
            new("note", "製造メモ", AllowText: true, When: answers => answers.TryGetValue("work", out var answer) && answer.Keys.Contains("production"))]);
        questionnaire.Submitted += (_, e) => _status.Text = $"{e.Answers.Count}項目の回答を受け取りました。";
        body.Add(questionnaire, UiLayout.Wrap(Ui.Button("回答を保存", () =>
        {
            using var save = new SaveFileDialog { Filter = "回答JSON|*.json" }; if (save.ShowDialog(this) == DialogResult.OK) questionnaire.SaveState(save.FileName);
        }), Ui.Button("回答を再開", () =>
        {
            using var open = new OpenFileDialog { Filter = "回答JSON|*.json" }; if (open.ShowDialog(this) == DialogResult.OK) questionnaire.RestoreState(open.FileName);
        })));
    }
    private void BuildMessages()
    {
        var body = Page("メッセージ"); var transcript = new MessageScroller(); var input = new TextBox { Width = 350 };
        var first = new ConversationMessage("welcome", "担当者", "実績を入力してください。", footer: Ui.Label("確認済み")); first.Bubble.AddReaction("確認", 2); transcript.Append(first);
        body.Add(UiLayout.Wrap(input, Ui.Button("送信", () =>
        {
            if (string.IsNullOrWhiteSpace(input.Text)) return;
            string id = $"user-{++_messageId}"; transcript.Append(new ConversationMessage(id, "作業者", input.Text, MessageAlignment.End), startTurn: true);
            input.Text = ""; transcript.Append(new ConversationMessage($"reply-{_messageId}", "担当者", "入力を受け取りました。")); transcript.CompleteTurn();
        }), Ui.Button("最新へ", transcript.FollowLatest), Ui.Button("履歴を追加", () => transcript.PrependHistory([new ConversationMessage($"history-{++_messageId}", "履歴", "前の作業記録です。")]))), transcript.FillRemainingHeight());
    }
    private void BuildNative()
    {
        var body = Page("標準部品"); var menu = new MenuStrip();
        var file = new ToolStripMenuItem("操作"); file.DropDownItems.Add("確認", null, (_, _) => _status.Text = "標準メニューを選択しました。"); menu.Items.Add(file);
        var check = new CheckBox { Text = "チェック / スイッチ", AutoSize = true };
        var toggle = new CheckBox { Text = "トグル", Appearance = Appearance.Button, AutoSize = true };
        var radio = UiLayout.Row(new RadioButton { Text = "選択A", AutoSize = true }, new RadioButton { Text = "選択B", AutoSize = true });
        var calendar = new MonthCalendar { MaxSelectionCount = 31, ShowWeekNumbers = true };
        var date = new DateTimePicker { Format = DateTimePickerFormat.Short };
        var time = new DateTimePicker { Format = DateTimePickerFormat.Time, ShowUpDown = true };
        var otp = new MaskedTextBox("000000") { AccessibleName = "6桁コード" };
        var progress = new ProgressBar { Value = 60 }; var spinner = new ProgressBar { Style = ProgressBarStyle.Marquee };
        var tree = new TreeView { Height = 130 }; var root = tree.Nodes.Add("工場"); root.Nodes.Add("ラインA"); root.Nodes.Add("ラインB"); root.Expand();
        tree.AfterSelect += (_, e) => _status.Text = e.Node?.Text ?? "";
        var split = new SplitContainer { Height = 120, Orientation = Orientation.Vertical };
        split.Panel1.Controls.Add(new Label { Text = "左ペイン", Dock = DockStyle.Fill }); split.Panel2.Controls.Add(new Label { Text = "右ペイン", Dock = DockStyle.Fill });
        var tooltip = new ToolTip(); _resources.Add(tooltip); tooltip.SetToolTip(check, "Windows Forms標準のヒントです。");
        var context = new ContextMenuStrip(); context.Items.Add("標準の右クリック操作", null, (_, _) => _status.Text = "右クリック操作を実行しました。"); tree.ContextMenuStrip = context; _resources.Add(context);
        var group = new GroupBox { Text = "Card / Fieldset / Empty", Height = 80 }; group.Controls.Add(new Label { Text = "未登録です。標準LabelとGroupBoxで表現できます。", Dock = DockStyle.Fill });
        body.Add(menu, UiLayout.Wrap(check, toggle), radio, UiLayout.Wrap(date, time, otp), calendar, progress, spinner, tree, split, group,
            UiLayout.Wrap(Ui.Button("確認ダイアログ", () => _status.Text = MessageBox.Show(this, "処理を続けますか？", "確認", MessageBoxButtons.YesNo).ToString()),
                Ui.Button("RTL切替", () => { RightToLeft = RightToLeft == RightToLeft.Yes ? RightToLeft.No : RightToLeft.Yes; RightToLeftLayout = RightToLeft == RightToLeft.Yes; })));
    }
    private void BuildGantt()
    {
        var body = Page("ガント");
        var tasks = SampleData.CreateSchedule();
        var gantt = new GanttChart().SetData(tasks);
        var scale = Field.Select("表示単位", new[] { ("Day", "日"), ("Week", "週"), ("Month", "月") });
        scale.ValueChanged += (_, _) => gantt.TimelineScale = Enum.Parse<GanttScale>(scale.Value);
        gantt.SelectionChanged += (_, _) => _status.Text = gantt.SelectedTask is { } task
            ? $"{task.Title} / {task.Start:MM/dd}〜{task.End:MM/dd} / 進捗 {task.Progress}%" : "作業を選択してください。";
        body.Add(UiLayout.Wrap(scale, Ui.Button("今日へ", () => gantt.ScrollToDate(DateTime.Today)),
            Ui.Button("全作業の期間", gantt.FitToTasks), Ui.Button("選択作業の進捗 +10%", () =>
            {
                if (gantt.SelectedTask is { } task) { task.Progress = Math.Min(100, task.Progress + 10); gantt.RefreshData(); _status.Text = $"{task.Title}: {task.Progress}%"; }
            })), Ui.Label("作業をクリック、または上下キーで選択できます。日付は終了日を含みます。"), gantt.FillRemainingHeight());
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _theme?.Dispose(); foreach (var resource in _resources) resource.Dispose(); }
        base.Dispose(disposing);
    }
}
