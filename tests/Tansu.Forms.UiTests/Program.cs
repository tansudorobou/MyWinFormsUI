using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Application = FlaUI.Core.Application;

var taskDirectory = new DirectoryInfo(AppContext.BaseDirectory);
while (taskDirectory is not null && !File.Exists(Path.Combine(taskDirectory.FullName, "Tansu.Forms.slnx"))) taskDirectory = taskDirectory.Parent;
string repository = taskDirectory?.FullName ?? throw new DirectoryNotFoundException("The solution directory was not found.");
string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
string executable = args.FirstOrDefault() ?? Path.Combine(repository, "samples", "Tansu.Forms.Sample", "bin", configuration, "net10.0-windows", "Tansu.Forms.Sample.exe");
string output = Path.Combine(repository, "artifacts", "ui-verification"); Directory.CreateDirectory(output);
var results = new List<string>();
using var automation = new UIA3Automation();
Application? app = null;
Window? window = null;
try
{
    // The target positions itself off-screen; SW_HIDE would suppress its native automation window entirely.
    app = Application.Launch(new ProcessStartInfo(executable, "--automation-sample") { UseShellExecute = false, CreateNoWindow = true });
    void Wait(Func<bool> condition, string message)
    {
        var timer = Stopwatch.StartNew();
        while (!condition()) { if (timer.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException(message); Thread.Sleep(50); }
    }
    // ShowInTaskbar=false forms have a hidden owner and are not Process.MainWindowHandle candidates.
    Wait(() =>
    {
        window = automation.GetDesktop().FindFirstChild(condition => condition.ByProcessId(app.ProcessId)
            .And(condition.ByName("Tansu.Forms UI Automation")))?.AsWindow();
        return window is not null;
    }, "The automation sample did not open.");
    AutomationElement Find(string id) => window!.FindFirstDescendant(condition => condition.ByAutomationId(id))
        ?? throw new InvalidOperationException($"UI Automation element not found: {id}");
    string Read(string id) => Find(id).Name;
    void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    var part = Find("PartNumber").AsTextBox();
    var quantity = Find("GoodCount").AsTextBox();
    var save = Find("production.save").AsButton();
    Check(part.Name == "品番" && part.Text == "P-INITIAL" && !save.IsEnabled, "Property-name IDs locate an editor with its independent display label and an initially disabled save command.");
    part.Text = "";
    Wait(() => Read("form.errors").Contains("PartNumber") && !save.IsEnabled, "Required-field validation was not exposed.");
    quantity.Text = "invalid";
    Wait(() => Read("form.errors").Contains("GoodCount"), "Conversion errors were not exposed.");
    part.Text = "P-UIA"; quantity.Text = "12";
    Wait(() => save.IsEnabled && Read("form.state") == "未保存の変更があります" && Read("form.errors") == "エラーなし", "Valid editing did not enable saving.");
    results.Add("PASS: External UIA value input locates fields by stable model-property IDs, preserves display labels and publishes required/conversion validation and dirty state.");
    save.Invoke();
    Wait(() => !save.IsEnabled && Read("operation.status") == "処理中", "Async command did not enter its busy state.");
    Check(save.Name == "処理中…" && !part.IsEnabled, "The busy button text and locked editor are visible to UI Automation.");
    Find("production.complete").AsButton().Invoke();
    Wait(() => Read("operation.status") == "登録済み: P-UIA/12" && Read("form.state") == "変更なし", "Registration did not complete and accept changes.");
    Check(!save.IsEnabled && part.IsEnabled && save.Name == "登録", "Save remains disabled after acceptance while the editor/button text are restored.");
    results.Add("PASS: External UIA button invocation executes the async action, exposes busy/disabled state and completes with accepted form values.");
    part.Text = "P-CANCEL"; Wait(() => save.IsEnabled, "Editing after save did not enable a new operation."); save.Invoke();
    Wait(() => Find("production.cancel").IsEnabled && Read("operation.status") == "処理中", "Cancellation was not enabled.");
    Find("production.cancel").AsButton().Invoke();
    Wait(() => Read("operation.status") == "取り消しました" && save.IsEnabled, "Cancellation did not restore execution eligibility.");
    Find("form.reset").AsButton().Invoke();
    Wait(() => part.Text == "P-UIA" && Read("form.state") == "変更なし" && !save.IsEnabled, "Reset did not return to the saved baseline.");
    Find("production.fail").AsButton().Invoke();
    Wait(() => Read("operation.status") == "失敗: テストエラー", "An async failure was not published.");
    Check(Find("production.fail").IsEnabled && !app.HasExited, "An async failure leaves the native UI usable.");
    results.Add("PASS: External UIA cancellation, reset and failure actions preserve a usable UI and the saved baseline.");
    results.Add("All external UI checks passed.");
}
catch (Exception error)
{
    if (app is not null) results.Add($"Target process {app.ProcessId}: exited={app.HasExited}");
    results.Add(error.ToString()); Environment.ExitCode = 1;
}
finally
{
    if (app is not null)
    {
        try
        {
            if (!app.HasExited)
            {
                window?.Close();
                using var process = Process.GetProcessById(app.ProcessId);
                if (!process.WaitForExit(3000)) app.Kill();
            }
        }
        catch (ArgumentException) when (app.HasExited) { } // The target can exit between Close and process lookup.
        catch (Exception error) { results.Add("Target cleanup failed: " + error); Environment.ExitCode = 1; if (!app.HasExited) app.Kill(); }
        finally { app.Dispose(); }
    }
    File.WriteAllLines(Path.Combine(output, "results.txt"), results);
    foreach (string result in results) Console.WriteLine(result);
}
