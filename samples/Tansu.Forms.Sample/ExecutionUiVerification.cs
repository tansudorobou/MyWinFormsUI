using System.ComponentModel.DataAnnotations;
using Tansu.Forms.Execution;

namespace Tansu.Forms.Sample;

internal static class ExecutionUiVerification
{
    internal static void Run(ICollection<string> results)
    {
        int uiThread = Environment.CurrentManagedThreadId;
        var handler = new WorkerAction(uiThread);
        using var form = new ActionForm(handler) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000) };
        var editor = new ModelForm<Input>(new() { Name = "draft" }); form.Controls.Add(editor);
        form.Show();
        var editorControl = (InputField<object?>)editor.Fields[nameof(Input.Name)];
        bool onUi = true; int notifications = 0;
        form.ActionStateChanged += (_, _) => { onUi &= Environment.CurrentManagedThreadId == uiThread; notifications++; };
        var get = Complete(form.Action.SelectAsync());
        Check(get.IsSuccess && notifications >= 2 && onUi && handler.ExecutedOffUiThread, "ActionForm executes ordinary async handlers off the UI context and marshals notifications back.");
        var failure = Complete(form.Action.InsertAsync(new Input { Name = "draft" }));
        editor.ApplyErrors(failure.FieldErrors, failure.Message);
        Check(editor.HasErrors && editor.IsValidated && editor.ErrorsByField.ContainsKey(nameof(Input.Name))
            && editorControl.ErrorMessage == "Duplicate name." && !editor.IsDirty,
            "Action field errors reach existing editors without replacing values or dirty state.");
        ((TextBox)editorControl.Editor).Text = "fixed";
        Check(!editor.HasErrors && editor.IsDirty && ReferenceEquals(editorControl, editor.Fields[nameof(Input.Name)]),
            "Editing clears stale action errors and retains the editor.");
        var pending = form.Action.DeleteAsync();
        handler.DeleteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        form.Dispose();
        // Do not pump UI messages here: a closing form's loop may already have stopped.
        try { pending.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult(); throw new InvalidOperationException("Disposal did not cancel the page action."); }
        catch (OperationCanceledException) { }
        Check(!form.Action.IsRunning && form.Action.IsCanceled, "Closing the form cancels its page action.");
        results.Add("PASS: ActionForm UI-thread notifications, business field errors, retained controls and lifetime cancellation.");

        using var stdout = new StringWriter(); using var stderr = new StringWriter();
        int code = Complete(new ToolRunner(new WorkerAction(uiThread)).RunAsync(["SELECT"], new StringReader(""), stdout, stderr));
        Check(code == 0 && stderr.ToString() == "", "The same page action also runs without creating a form.");

        using var demo = new ExecutionDemoForm { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000) };
        var matches = Complete(demo.Action.SelectAsync(new Dictionary<string, string> { ["name"] = "品番", ["minQuantity"] = "10" })).ReadBody<List<ExecutionDemoForm.Entry>>();
        Check(matches.Count == 1 && matches[0].Quantity == 10, "Gallery SELECT accepts combined name and quantity conditions.");
        matches = Complete(demo.Action.SelectAsync(new Dictionary<string, string> { ["name"] = "品番", ["minQuantity"] = "11" })).ReadBody<List<ExecutionDemoForm.Entry>>();
        Check(matches.Count == 0, "Gallery SELECT applies all conditions.");
        Check(!Complete(demo.Action.SelectAsync(new Dictionary<string, string> { ["minQuantity"] = "bad" })).IsSuccess,
            "Gallery SELECT rejects malformed quantity conditions.");
        results.Add("PASS: Gallery SELECT combines search conditions and validates parameter values.");
    }
    private static T Complete<T>(Task<T> task)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted) { Application.DoEvents(); if (timer.ElapsedMilliseconds > 5000) throw new TimeoutException("Execution UI verification timed out."); Thread.Sleep(5); }
        Application.DoEvents(); return task.GetAwaiter().GetResult();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Input { [Required] public string Name { get; set; } = ""; }
    private sealed class WorkerAction(int uiThread) : IPageAction
    {
        public bool ExecutedOffUiThread { get; private set; }
        public TaskCompletionSource DeleteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<ActionResponse> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
        {
            ExecutedOffUiThread = Environment.CurrentManagedThreadId != uiThread && SynchronizationContext.Current is null;
            if (request.Method == ActionMethod.Delete) { DeleteStarted.TrySetResult(); await Task.Delay(Timeout.Infinite, cancellationToken); }
            else await Task.Delay(20, cancellationToken);
            return request.Method == ActionMethod.Insert
                ? ActionResponse.ValidationFailed(new Dictionary<string, string[]> { [nameof(Input.Name)] = ["Duplicate name."] })
                : ActionResponse.Ok(new { Name = "result" });
        }
    }
}
