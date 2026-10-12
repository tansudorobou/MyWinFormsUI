using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Tansu.Forms.Execution;
using UiLayout = Tansu.Forms.Layout.Layout;

namespace Tansu.Forms.Sample;

/// <summary>A small GUI sharing one SQL-style operation action. Gallery data deliberately lives only in this window.</summary>
public sealed class ExecutionDemoForm : ActionForm
{
    public ExecutionDemoForm() : base(new DemoAction())
    {
        Text = "SELECT / INSERT / UPDATE — 共通Action";
        var editor = new ModelForm<Entry>(new() { Name = "品番A", Quantity = 10 }) { ValidateOnChange = true };
        var grid = new DataGrid<Entry>();
        var search = Field.Text("品番検索"); search.Editor.Name = "Name.search";
        var minimum = Field.Number("数量の下限", min: 0, max: 10000); minimum.Editor.Name = "Quantity.min";
        Dictionary<string, string> Conditions() => new()
        {
            ["name"] = search.Value,
            ["minQuantity"] = minimum.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        var status = Elements.Label("データはこのウィンドウ内に保持します。一覧の行をダブルクリックして編集できます。");
        var select = Elements.ButtonAsync("SELECT 検索", async token =>
        {
            var response = await Action.SelectAsync(Conditions(), cancellationToken: token);
            if (!response.IsSuccess) { status.Text = response.Message; return; }
            grid.SetData(response.ReadBody<List<Entry>>()); status.Text = "取得しました。";
        }, () => !Action.IsRunning);
        AsyncButton SaveButton(string text, bool update) => Elements.ButtonAsync(text, async token =>
        {
            if (!editor.TryRead(out var input)) { editor.FocusFirstError(); return; }
            editor.Enabled = false;
            try
            {
                var response = update
                    ? await Action.UpdateAsync(input, parameters: new Dictionary<string, string> { ["id"] = input!.Id.ToString() }, cancellationToken: token)
                    : await Action.InsertAsync(input, cancellationToken: token);
                if (!response.IsSuccess)
                {
                    editor.ApplyErrors(response.FieldErrors, response.Message); status.Text = response.Message; return;
                }
                editor.LoadModel(response.ReadBody<Entry>());
                // Re-query after the write; do not re-run the mutation if a refresh fails.
                try { grid.SetData((await Action.SelectAsync(Conditions())).ReadBody<List<Entry>>()); status.Text = "保存しました。"; }
                catch (Exception error) { status.Text = $"保存済み。再取得に失敗: {error.Message}"; }
            }
            finally { if (!editor.IsDisposed) { editor.Enabled = true; if (editor.HasErrors) editor.FocusFirstError(); } }
        }, () => !Action.IsRunning);
        var insert = SaveButton("INSERT 登録", false); var update = SaveButton("UPDATE 更新", true);
        var cancel = Elements.Button("取り消し", Action.Cancel);
        var buttons = new[] { select, insert, update };
        foreach (var button in buttons) button.ExecutionFailed += (_, e) => { if (!IsDisposed) status.Text = e.Exception.Message; };
        ActionStateChanged += (_, _) =>
        {
            foreach (var button in buttons) button.AsyncCommand.NotifyCanExecuteChanged();
            cancel.Enabled = Action.IsRunning;
            if (Action.IsRunning) status.Text = $"{Action.PendingRequest!.Method} 実行中…";
            else if (Action.IsCanceled) status.Text = "取り消しました。";
        };
        grid.Grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && !Action.IsRunning && grid.SelectedItem is { } item) editor.LoadModel(item); };
        UiLayout.Root(this, RootSize.Standard, editor, UiLayout.AutoGrid(search, minimum), UiLayout.Row(select, insert, update, cancel), status, grid.FillRemainingHeight());
        Shown += (_, _) => select.PerformClick();
    }
    public sealed class Entry
    {
        [Browsable(false)] public int Id { get; set; }
        [Required, DisplayName("品番")] public string Name { get; set; } = "";
        [Range(0, 10000), DisplayName("数量")] public int Quantity { get; set; }
    }
    private sealed class DemoAction : IPageAction
    {
        private readonly List<Entry> _records = [new() { Id = 1, Name = "品番A", Quantity = 10 }];
        public async Task<ActionResponse> ExecuteAsync(ActionRequest request, CancellationToken token)
        {
            await Task.Delay(300, token);
            if (request.Method == ActionMethod.Select)
            {
                int minimum = 0;
                if (request.Parameters.TryGetValue("minQuantity", out string? rawMinimum)
                    && (!int.TryParse(rawMinimum, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out minimum) || minimum is < 0 or > 10000))
                    return ActionResponse.BadRequest("minQuantityは0から10000の整数を指定してください。");
                IEnumerable<Entry> matches = _records.Where(item => item.Quantity >= minimum);
                if (request.Parameters.TryGetValue("name", out string? name)) matches = matches.Where(item => item.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
                return ActionResponse.Ok(matches.ToArray());
            }
            if (request.Method is not (ActionMethod.Insert or ActionMethod.Update)) return ActionResponse.MethodNotAllowed();
            var input = request.ReadBody<Entry>();
            if (input.Name == "重複") return ActionResponse.ValidationFailed(new Dictionary<string, string[]> { [nameof(Entry.Name)] = ["この品番は登録済みです。"] });
            if (request.Method == ActionMethod.Update)
            {
                if (!request.Parameters.TryGetValue("id", out string? raw) || !int.TryParse(raw, out int id)) return ActionResponse.BadRequest("更新対象のidが必要です。");
                var target = _records.FirstOrDefault(item => item.Id == id);
                if (target is null) return ActionResponse.NotFound("更新対象を一覧から選んでください。");
                target.Name = input.Name; target.Quantity = input.Quantity; return ActionResponse.Ok(target);
            }
            var created = new Entry { Id = _records.Max(item => item.Id) + 1, Name = input.Name, Quantity = input.Quantity };
            _records.Add(created); return ActionResponse.Created(created);
        }
    }
}
