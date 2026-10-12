#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:property OutputType=Exe
#:property DisableWinExeOutputInference=true
#:property PublishAot=false
#:project ../../src/Tansu.Forms.Components/Tansu.Forms.Components.csproj

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Tansu.Forms.Components;
using Tansu.Forms.Execution;
using Tansu.Forms.Layout;
using UiLayout = Tansu.Forms.Layout.Layout;

// CLI execution never enters this GUI callback. --verify only verifies the GUI startup.
bool verify = args is ["--verify"];
return await ActionApp.RunAsync(verify ? [] : args, new ProductionAction(), form =>
{
    form.Text = "1ファイルのGUI・CLI共通Action";
    var editor = new ModelForm<ProductionEntry>(new() { PartNumber = "P-102", GoodCount = 100 }) { ValidateOnChange = true };
    var grid = new DataGrid<ProductionEntry>();
    var partSearch = Field.Text("品番検索"); partSearch.Editor.Name = "PartNumber.search";
    var minimum = Field.Number("良品数の下限", min: 0, max: 1_000_000); minimum.Editor.Name = "GoodCount.min";
    Dictionary<string, string> Conditions() => new()
    {
        ["partNumber"] = partSearch.Value,
        ["minGoodCount"] = minimum.Value.ToString(CultureInfo.InvariantCulture)
    };
    var status = Elements.Label("SELECTで条件検索、INSERTで登録、UPDATEで更新できます。");
    async Task ShowResult(ActionResponse response, bool saved)
    {
        if (!response.IsSuccess)
        {
            editor.ApplyErrors(response.FieldErrors, response.Message);
            status.Text = response.Message; return;
        }
        if (saved) editor.LoadModel(response.ReadBody<ProductionEntry>());
        try
        {
            var list = await form.Action.SelectAsync(Conditions());
            if (list.IsSuccess) grid.SetData(list.ReadBody<List<ProductionEntry>>());
            status.Text = list.IsSuccess ? "表示を更新しました。" : "保存済みですが一覧の再取得に失敗しました。";
        }
        catch (Exception error) { if (!form.IsDisposed) status.Text = $"保存済み。再取得に失敗: {error.Message}"; }
    }
    var load = Elements.ButtonAsync("SELECT 検索", async token =>
    {
        var response = await form.Action.SelectAsync(Conditions(), cancellationToken: token);
        if (response.IsSuccess) grid.SetData(response.ReadBody<List<ProductionEntry>>());
        status.Text = response.IsSuccess ? "取得しました。" : response.Message;
    }, () => !form.Action.IsRunning);
    var create = Elements.ButtonAsync("INSERT 登録", async token =>
    {
        if (!editor.TryRead(out var value)) { editor.FocusFirstError(); return; }
        editor.Enabled = false;
        try { await ShowResult(await form.Action.InsertAsync(value, cancellationToken: token), saved: true); }
        finally { if (!editor.IsDisposed) { editor.Enabled = true; if (editor.HasErrors) editor.FocusFirstError(); } }
    }, () => !form.Action.IsRunning);
    var update = Elements.ButtonAsync("UPDATE 更新", async token =>
    {
        if (!editor.TryRead(out var value)) { editor.FocusFirstError(); return; }
        if (value!.Id == 0) { status.Text = "一覧の行をダブルクリックして更新対象を選んでください。"; return; }
        editor.Enabled = false;
        try { await ShowResult(await form.Action.UpdateAsync(value, parameters: new Dictionary<string, string>
            { ["id"] = value.Id.ToString(CultureInfo.InvariantCulture) }, cancellationToken: token), saved: true); }
        finally { if (!editor.IsDisposed) { editor.Enabled = true; if (editor.HasErrors) editor.FocusFirstError(); } }
    }, () => !form.Action.IsRunning);
    foreach (var button in new[] { load, create, update })
        button.ExecutionFailed += (_, e) => { if (!form.IsDisposed) status.Text = e.Exception.Message; };
    form.ActionStateChanged += (_, _) =>
    {
        foreach (var button in new[] { load, create, update }) button.AsyncCommand.NotifyCanExecuteChanged();
    };
    grid.Grid.CellDoubleClick += (_, e) =>
    {
        if (e.RowIndex >= 0 && !form.Action.IsRunning && grid.SelectedItem is { } selected) editor.LoadModel(selected);
    };
    UiLayout.Root(form, RootSize.Workspace, editor, UiLayout.AutoGrid(partSearch, minimum), UiLayout.Row(load, create, update), status, grid.FillRemainingHeight());
    form.Shown += (_, _) =>
    {
        if (verify) form.BeginInvoke((Action)(() => form.Close()));
        else load.PerformClick();
    };
    if (verify)
    {
        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-10000, -10000);
    }
});

public sealed class ProductionEntry
{
    [Browsable(false)] public int Id { get; set; }
    [Required, DisplayName("品番")] public string PartNumber { get; set; } = "";
    [Range(0, 1_000_000), DisplayName("良品数")] public int GoodCount { get; set; }
}

public sealed class ProductionAction : IPageAction
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<ActionResponse> ExecuteAsync(ActionRequest request, CancellationToken token)
    {
        if (request.Path != "/") return ActionResponse.NotFound();
        if (request.Method is not (ActionMethod.Select or ActionMethod.Insert or ActionMethod.Update or ActionMethod.Delete))
            return ActionResponse.MethodNotAllowed();
        int? id = null;
        if (request.Parameters.TryGetValue("id", out string? rawId))
        {
            if (!int.TryParse(rawId, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) || parsed <= 0)
                return ActionResponse.BadRequest("id must be a positive integer.");
            id = parsed;
        }
        var input = request.Method is ActionMethod.Insert or ActionMethod.Update ? request.ReadBody<ProductionEntry>() : null;
        int minimum = 0, maximum = 1_000_000;
        if (request.Method == ActionMethod.Select)
        {
            minimum = ReadCount("minGoodCount", 0); maximum = ReadCount("maxGoodCount", 1_000_000);
            if (minimum > maximum) return ActionResponse.BadRequest("minGoodCount must not exceed maxGoodCount.");
        }
        string store = request.Parameters.TryGetValue("store", out string? supplied) ? Path.GetFullPath(supplied)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tansu.Forms", "single-file", "production.json");
        Directory.CreateDirectory(Path.GetDirectoryName(store)!);
        // Fail with a conflict if another process is using this store, instead of losing its writes.
        FileStream guard;
        try { guard = new FileStream(store + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { return new(409, message: "The store is busy. Retry the request."); }
        using (guard)
        {
            var records = File.Exists(store)
                ? JsonSerializer.Deserialize<List<ProductionEntry>>(await File.ReadAllTextAsync(store, token), Json)
                    ?? throw new InvalidDataException("The store contains null.")
                : [new() { Id = 1, PartNumber = "P-102", GoodCount = 120 }, new() { Id = 2, PartNumber = "P-205", GoodCount = 80 }];
            var target = records.FirstOrDefault(record => record.Id == id);
            switch (request.Method)
            {
                case ActionMethod.Select:
                    IEnumerable<ProductionEntry> matches = records;
                    if (id.HasValue) matches = matches.Where(record => record.Id == id.Value);
                    if (request.Parameters.TryGetValue("partNumber", out string? part))
                        matches = matches.Where(record => record.PartNumber.Contains(part, StringComparison.OrdinalIgnoreCase));
                    matches = matches.Where(record => record.GoodCount >= minimum && record.GoodCount <= maximum);
                    if (id.HasValue)
                    {
                        var selected = matches.FirstOrDefault();
                        return selected is null ? ActionResponse.NotFound() : ActionResponse.Ok(selected);
                    }
                    return ActionResponse.Ok(matches.ToArray());
                case ActionMethod.Insert:
                    var created = new ProductionEntry { Id = checked(records.Select(record => record.Id).DefaultIfEmpty().Max() + 1),
                        PartNumber = input!.PartNumber, GoodCount = input.GoodCount };
                    records.Add(created); await Save(); return ActionResponse.Created(created);
                case ActionMethod.Update:
                    if (!id.HasValue) return ActionResponse.BadRequest("UPDATE requires --id.");
                    if (target is null) return ActionResponse.NotFound();
                    target.PartNumber = input!.PartNumber; target.GoodCount = input.GoodCount;
                    await Save(); return ActionResponse.Ok(target);
                case ActionMethod.Delete:
                    if (!id.HasValue) return ActionResponse.BadRequest("DELETE requires --id.");
                    if (target is null) return ActionResponse.NotFound();
                    records.Remove(target); await Save(); return ActionResponse.Ok(new { Id = id.Value, Deleted = true });
                default: return ActionResponse.MethodNotAllowed();
            }
            async Task Save()
            {
                string temporary = store + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(records, Json), token);
                    token.ThrowIfCancellationRequested();
                    File.Move(temporary, store, overwrite: true);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }
        int ReadCount(string name, int fallback)
        {
            if (!request.Parameters.TryGetValue(name, out string? raw)) return fallback;
            if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count is < 0 or > 1_000_000)
                throw new ActionInputException($"{name} must be an integer from 0 to 1000000.");
            return count;
        }
    }
}
