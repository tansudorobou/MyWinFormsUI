using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Tansu.Forms.Execution;

var results = new List<string>();
void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
async Task<(int Code, string Output, string Error)> Run(IPageAction action, string[] arguments, string input = "", CancellationToken token = default)
{
    using var stdout = new StringWriter(); using var stderr = new StringWriter(); using var stdin = new StringReader(input);
    int code = await new ToolRunner(action).RunAsync(arguments, stdin, stdout, stderr, token);
    return (code, stdout.ToString(), stderr.ToString());
}
try
{
    var echo = new EchoAction();
    using var client = new ActionClient(echo);
    await client.SelectAsync(new Dictionary<string, string> { ["Name"] = "品番A", ["minQuantity"] = "10" }, path: "/records");
    Check(echo.Last!.Method == ActionMethod.Select && echo.Last.Parameters["name"] == "品番A"
        && echo.Last.Parameters["MINQUANTITY"] == "10", "SelectAsync passes multiple case-insensitive conditions to the handler.");
    var dto = new Input { Name = "品番A", Quantity = 12 };
    var created = await client.InsertAsync(dto);
    Check(created.StatusCode == 201 && ReferenceEquals(created.Body, dto), "In-process calls retain typed DTOs.");
    foreach (ActionMethod method in Enum.GetValues<ActionMethod>())
    {
        var response = await client.ExecuteAsync(new(method, dto, "/records/42", new Dictionary<string, string> { ["id"] = "42" }));
        Check(response.IsSuccess && echo.Last!.Method == method && echo.Last.Path == "/records/42" && echo.Last.Parameters["id"] == "42", "Every method reaches the single handler with its target/query.");
    }
    var invalid = await client.InsertAsync(new Input { Name = "", Quantity = -1 });
    Check(invalid.StatusCode == 422 && invalid.FieldErrors.ContainsKey(nameof(Input.Name)) && invalid.FieldErrors.ContainsKey(nameof(Input.Quantity)), "DTO annotation errors are returned with property-name keys.");
    Check((await client.InsertAsync(new { name = "anonymous", quantity = 3 })).ReadBody<Input>().Name == "anonymous", "Anonymous values bind to the action DTO.");
    results.Add("PASS: One action handles all methods, paths, query parameters, typed bodies and field validation.");

    var result = await Run(echo, ["SELECT", "/records", "--param", "name=品番 A=x", "--id", "42"]);
    using (var json = JsonDocument.Parse(result.Output))
        Check(result.Code == 0 && result.Error == "" && json.RootElement.GetProperty("path").GetString() == "/records"
            && echo.Last!.Parameters["name"] == "品番 A=x", "SELECT outputs one JSON document, preserving Unicode/query values.");
    result = await Run(echo, ["INSERT", "--body", "-"], "{\"name\":\"パイプ\",\"quantity\":7}");
    Check(result.Code == 0 && result.Error == "" && JsonSerializer.Deserialize<Input>(result.Output, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Name == "パイプ", "Stdin JSON binds to a typed INSERT and stdout contains only the saved data.");
    result = await Run(echo, ["UPDATE", "--body", result.Output]);
    Check(result.Code == 0 && result.Error == "" && echo.Last!.Method == ActionMethod.Update, "The previous output can become the next request body without an envelope.");
    result = await Run(echo, ["INSERT", "--body", "{\"name\":\"test\",\"quantity\":2}", "--envelope"]);
    using (var json = JsonDocument.Parse(result.Output))
        Check(json.RootElement.GetProperty("statusCode").GetInt32() == 201 && json.RootElement.GetProperty("body").GetProperty("quantity").GetInt32() == 2, "Envelope is opt-in.");
    string file = Path.GetTempFileName();
    try
    {
        await File.WriteAllTextAsync(file, "{\"name\":\"file\",\"quantity\":5}");
        result = await Run(echo, ["INSERT", "--body-file", file]);
        Check(result.Code == 0 && result.Output.Contains("file"), "JSON files can supply request bodies.");
    }
    finally { File.Delete(file); }
    result = await Run(echo, ["INSERT", "--body-file", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json")]);
    Check(result.Code == 2 && result.Output == "", "Missing body files are input failures.");
    results.Add("PASS: CLI query, Unicode, stdin, body files, output chaining and optional response envelope.");

    foreach (var arguments in new string[][] { [], ["unknown"], ["0"], ["INSERT", "--body"], ["INSERT", "--body", "{}", "--body", "{}"],
        ["SELECT", "--body", "{}"], ["SELECT", "--id", "1", "--param", "id=2"], ["SELECT", "--id", "1", "--param", "ID=2"],
        ["SELECT", "--param", "bad"], ["SELECT", "--path", "bad"], ["SELECT", "--unknown"],
        ["GET"], ["POST"], ["PUT"], ["PATCH"], ["SELECT", "--query", "name=test"] })
    {
        int calls = echo.Calls;
        result = await Run(echo, arguments);
        Check(result.Code == 2 && result.Output == "" && JsonDocument.Parse(result.Error).RootElement.GetProperty("statusCode").GetInt32() == 400 && echo.Calls == calls, "Bad arguments do not invoke the action or contaminate stdout.");
    }
    result = await Run(echo, ["select", "--param", "Name=品番A", "--param", "minQuantity=10"]);
    Check(result.Code == 0 && echo.Last!.Parameters["name"] == "品番A" && echo.Last.Parameters["minQuantity"] == "10",
        "Lowercase SQL operations and multiple search conditions bind correctly.");
    foreach (string body in new[] { "", "bad json", "{} {}", "null", "[]", "{\"name\":\"bad\",\"quantity\":\"not a number\"}" })
    {
        result = await Run(echo, ["INSERT", "--body", "-"], body);
        Check(result.Code == 2 && result.Output == "", "Bad/empty/mismatched bodies fail with no success output.");
    }
    result = await Run(echo, ["INSERT", "--body", "{\"name\":\"\",\"quantity\":-1}"]);
    using (var json = JsonDocument.Parse(result.Error))
        Check(result.Code == 2 && result.Output == "" && json.RootElement.GetProperty("fieldErrors").TryGetProperty("Name", out _), "Validation errors use stderr and a nonzero exit code.");
    foreach (var (status, exit) in new[] { (404, 3), (405, 4), (409, 5), (500, 1) })
    {
        result = await Run(new DelegateAction((_, _) => Task.FromResult(new ActionResponse(status, message: "failed"))), ["SELECT"]);
        Check(result.Code == exit && result.Output == "" && result.Error.Contains("failed"), "Status codes map to useful process exit codes.");
    }
    result = await Run(new DelegateAction((_, _) => throw new InvalidOperationException("execution failed")), ["SELECT"]);
    Check(result.Code == 1 && result.Output == "" && result.Error.Contains("execution failed"), "Unexpected failures remain off stdout.");
    result = await Run(new DelegateAction((_, _) => { object[] cycle = new object[1]; cycle[0] = cycle; return Task.FromResult(ActionResponse.Ok(cycle)); }), ["SELECT"]);
    Check(result.Code == 1 && result.Output == "", "Serialization failures leave no partial JSON document.");
    results.Add("PASS: Invalid arguments/JSON/DTOs, missing targets, unsupported methods, conflicts and exceptions use structured stderr/exit codes.");

    int positionalCalls = 0;
    var positional = PageAction.Insert<Input>(value => { positionalCalls++; return ActionResponse.Created(value); });
    result = await Run(positional, ["Insert", "--body", "東京 本社", "7", "--envelope"]);
    using (var json = JsonDocument.Parse(result.Output))
        Check(result.Code == 0 && json.RootElement.GetProperty("body").GetProperty("name").GetString() == "東京 本社"
            && json.RootElement.GetProperty("body").GetProperty("quantity").GetInt32() == 7, "Positional strings and numbers bind in model order.");
    foreach (var values in new[] { new[] { "name" }, new[] { "name", "2", "extra" }, new[] { "name", "not-a-number" }, new[] { "name", "101" } })
    {
        int before = positionalCalls;
        result = await Run(positional, ["INSERT", "--body", .. values]);
        Check(result.Code == 2 && result.Output == "" && positionalCalls == before, "Count, conversion and annotation errors never call Insert.");
    }
    result = await Run(positional, ["INSERT", "--body", "{\"name\":\"JSON\",\"quantity\":3}"]);
    Check(result.Code == 0 && result.Output.Contains("JSON"), "Typed actions retain JSON input support.");
    result = await Run(positional, ["INSERT", "--body", "-"], "{\"name\":\"stdin\",\"quantity\":4}");
    Check(result.Code == 0 && result.Output.Contains("stdin"), "Typed actions retain stdin JSON support.");
    result = await Run(echo, ["INSERT", "--body", "{\"name\":\"path\",\"quantity\":2}", "/records"]);
    Check(result.Code == 0 && echo.Last!.Path == "/records", "Trailing paths after JSON remain supported.");
    results.Add("PASS: Space-separated typed body values, numeric conversion, count/validation rejection, JSON/stdin compatibility and trailing JSON paths.");

    var actions = new PageAction();
    int saves = 0, pings = 0;
    ActionResponse Save(Input input) { saves++; return ActionResponse.Ok(input); }
    ActionResponse Ping() { pings++; return ActionResponse.Ok(new { Alive = true }); }
    async Task<ActionResponse> Rename(RenameInput input, CancellationToken token)
    { await Task.Delay(1, token); return ActionResponse.Ok(input); }
    using var save = actions.Subscribe<Input>(Save, description: "入力を保存");
    using var ping = actions.Subscribe(Ping);
    using var rename = actions.Subscribe<RenameInput>(Rename);
    Check(save.Name == "Save" && ping.Name == "Ping" && rename.Name == "Rename", "Local function names are inferred without SQL verbs or strings.");
    using var functionClient = new ActionClient(actions);
    Check(ReferenceEquals((await functionClient.ExecuteAsync(save, dto)).Body, dto)
        && (await functionClient.ExecuteAsync(ping)).IsSuccess, "GUI/client dispatch uses subscription objects without function names.");
    Check((await functionClient.ExecuteAsync(save, new Input())).StatusCode == 422 && saves == 1, "Subscribed functions share DTO annotation validation.");
    result = await Run(actions, ["sAvE", "--body", "東京 本社", "7"]);
    Check(result.Code == 0 && result.Error == "" && JsonDocument.Parse(result.Output).RootElement.GetProperty("name").GetString() == "東京 本社" && saves == 2, "CLI dispatches arbitrary inferred function names case-insensitively.");
    result = await Run(actions, ["Rename", "--body", "新しい名前"]);
    Check(result.Code == 0 && JsonDocument.Parse(result.Output).RootElement.GetProperty("title").GetString() == "新しい名前", "Each async function uses its own positional DTO shape.");
    result = await Run(actions, ["Save", "--body", "-"], "{\"name\":\"stdin\",\"quantity\":4}");
    Check(result.Code == 0 && result.Output.Contains("stdin"), "Subscribed functions accept piped JSON.");
    result = await Run(actions, ["--help"]);
    Check(result.Code == 0 && result.Error == "" && result.Output.Contains("Save --body <name> <quantity>")
        && result.Output.Contains("Rename --body <title>") && result.Output.Contains("入力を保存")
        && result.Output.Contains("Ping") && !result.Output.Contains("INSERT") && pings == 1, "Help lists only subscriptions with field order and description and invokes nothing.");
    result = await Run(actions, ["DELETE"]);
    Check(result.Code == 2 && result.Output == "" && result.Error.Contains("--help"), "Unregistered SQL verbs cannot trigger arbitrary functions.");
    result = await Run(actions, ["Ping", "--body", "{}"]);
    Check(result.Code == 2 && pings == 1, "No-input functions reject accidental bodies before execution.");
    try { actions.Subscribe<Input>(Save, name: "save"); throw new Exception("Duplicate subscription was accepted."); }
    catch (ArgumentException) { }
    ping.Dispose();
    result = await Run(actions, ["-h"]);
    Check(!result.Output.Contains("Ping"), "Unsubscribing updates help immediately.");
    using var replacement = actions.Subscribe(Ping);
    ping.Dispose();
    Check((await functionClient.ExecuteAsync(ping)).StatusCode == 405
        && (await functionClient.ExecuteAsync(replacement)).IsSuccess, "Stale subscription handles cannot invoke or remove a replacement.");
    using var otherClient = new ActionClient(new PageAction());
    Check((await otherClient.ExecuteAsync(save, dto)).StatusCode == 405, "Subscriptions cannot be executed against another registry.");
    using var customSelect = actions.Subscribe<Input>(Save, name: "Select");
    result = await Run(actions, ["Select", "--body", "custom", "5"]);
    Check(result.Code == 0, "Registered names do not inherit SQL verb body restrictions.");
    using var wait = actions.Subscribe(async (CancellationToken token) =>
    { await Task.Delay(Timeout.Infinite, token); return ActionResponse.Ok(); }, name: "Wait");
    using var functionCancellation = new CancellationTokenSource();
    var functionPending = functionClient.ExecuteAsync(wait, functionCancellation.Token);
    Check(functionClient.PendingRequest?.Function == "Wait", "Pending function is observable.");
    functionCancellation.Cancel();
    try { await functionPending; throw new Exception("Subscribed cancellation was ignored."); } catch (OperationCanceledException) { }
    Check(functionClient.IsCanceled && !functionClient.IsRunning, "Subscribed async functions retain cancellation and state tracking.");
    results.Add("PASS: Arbitrary subscriptions, name-free GUI execution, inferred CLI names/help, per-function bodies, validation, unsubscription, duplicates and cancellation.");

    using var factorySave = PageAction.Create<Input>(Save, description: "保存する関数");
    using var independentSave = PageAction.Create<Input>(Save);
    using var factoryClient = new ActionClient(factorySave);
    Check(ReferenceEquals((await factoryClient.ExecuteAsync(factorySave, dto)).Body, dto), "Factory results are directly usable as both page actions and typed execution handles.");
    result = await Run(factorySave, ["Save", "--body", "factory", "6"]);
    Check(result.Code == 0 && result.Output.Contains("factory"), "Factory results directly support CLI positional execution.");
    result = await Run(factorySave, ["--help"]);
    Check(result.Output.Contains("Save --body <name> <quantity>") && result.Output.Contains("保存する関数")
        && !result.Output.Contains("Ping"), "Factory help advertises its own inferred function and metadata.");
    factorySave.Dispose();
    Check((await factoryClient.ExecuteAsync(factorySave, dto)).StatusCode == 405, "Disposing a factory registration prevents further execution.");
    result = await Run(independentSave, ["Save", "--body", "independent", "8"]);
    Check(result.Code == 0, "Factories isolate registrations even when function names match.");
    using var factoryPing = PageAction.Create(Ping);
    result = await Run(factoryPing, ["Ping"]);
    Check(result.Code == 0, "No-input factory functions are directly callable from the CLI.");
    using var factoryAsync = PageAction.Create<RenameInput>(Rename);
    result = await Run(factoryAsync, ["Rename", "--body", "async"]);
    Check(result.Code == 0 && result.Output.Contains("async"), "Async factories retain typed body binding and function names.");
    results.Add("PASS: Static factories combine creation/subscription, execute directly from client/CLI, retain help and disposal, and isolate matching function names.");

    using var started = new CancellationTokenSource();
    var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using var pendingClient = new ActionClient(new DelegateAction(async (_, token) =>
    {
        entered.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return ActionResponse.Ok();
    }));
    var running = pendingClient.SelectAsync(); await entered.Task;
    Check(pendingClient.IsRunning && pendingClient.PendingRequest!.Method == ActionMethod.Select, "Pending method is observable.");
    try { await pendingClient.InsertAsync(dto); throw new Exception("Duplicate action ran."); }
    catch (InvalidOperationException) { }
    pendingClient.Cancel();
    try { await running; throw new Exception("Cancellation was ignored."); } catch (OperationCanceledException) { }
    Check(!pendingClient.IsRunning && pendingClient.IsCanceled && pendingClient.LastError is null, "Cancellation restores the client without a failure.");
    var disposing = pendingClient.SelectAsync(); pendingClient.Dispose();
    try { await disposing; throw new Exception("Dispose did not cancel."); } catch (OperationCanceledException) { }
    try { await pendingClient.SelectAsync(); throw new Exception("Disposed client ran."); } catch (ObjectDisposedException) { }
    started.Cancel(); int beforeCanceled = echo.Calls;
    result = await Run(echo, ["SELECT"], token: started.Token);
    Check(result.Code == 130 && result.Output == "" && echo.Calls == beforeCanceled, "Pre-cancellation never invokes the action.");
    results.Add("PASS: Page-wide duplicate rejection, cancellation, disposal and canceled CLI requests.");
    bool shouldFail = true;
    using var retryClient = new ActionClient(new DelegateAction((_, _) => shouldFail
        ? throw new InvalidOperationException("retryable failure") : Task.FromResult(ActionResponse.Ok(dto))));
    try { await retryClient.SelectAsync(); throw new Exception("The handler failure disappeared."); }
    catch (InvalidOperationException failure) when (failure.Message == "retryable failure") { }
    Check(!retryClient.IsRunning && retryClient.LastError?.Message == "retryable failure", "Unexpected failures remain observable and restore idle.");
    shouldFail = false;
    Check((await retryClient.SelectAsync()).IsSuccess && retryClient.LastError is null, "A subsequent request clears the previous execution failure.");
    Check(!typeof(IPageAction).Assembly.GetReferencedAssemblies().Any(reference => reference.Name!.Contains("Windows.Forms")), "Execution has no WinForms dependency.");
    results.Add("All execution checks passed.");
}
catch (Exception failure) { results.Add(failure.ToString()); Environment.ExitCode = 1; }
Directory.CreateDirectory("artifacts/execution-verification");
await File.WriteAllLinesAsync("artifacts/execution-verification/results.txt", results);
foreach (string line in results) Console.WriteLine(line);

sealed class Input
{
    [Required] public string Name { get; set; } = "";
    [Range(0, 100)] public int Quantity { get; set; }
}
sealed class RenameInput
{
    [Required] public string Title { get; set; } = "";
}
sealed class EchoAction : IPageAction
{
    public int Calls { get; private set; }
    public ActionRequest? Last { get; private set; }
    public Task<ActionResponse> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        Calls++; Last = request;
        return Task.FromResult(request.Method == ActionMethod.Select || request.Method == ActionMethod.Delete
            ? ActionResponse.Ok(new { request.Path, request.Parameters })
            : new ActionResponse(request.Method == ActionMethod.Insert ? 201 : 200, request.ReadBody<Input>()));
    }
}
sealed class DelegateAction(Func<ActionRequest, CancellationToken, Task<ActionResponse>> execute) : IPageAction
{
    public Task<ActionResponse> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken) => execute(request, cancellationToken);
}
