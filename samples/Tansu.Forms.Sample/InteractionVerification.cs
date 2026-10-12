using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Input;

namespace Tansu.Forms.Sample;

internal static class InteractionVerification
{
    internal static void Run(ICollection<string> results)
    {
        using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
            Location = new Point(-10000, -10000), ClientSize = new Size(800, 600) };
        host.Show();
        void Mount(Control control)
        {
            foreach (Control previous in host.Controls.Cast<Control>().ToArray()) previous.Dispose();
            control.Dock = DockStyle.Fill; host.Controls.Add(control); host.PerformLayout(); Application.DoEvents();
        }

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int uiThread = Environment.CurrentManagedThreadId;
        int executions = 0;
        var button = Elements.ButtonAsync("登録", async token =>
        {
            executions++; await gate.Task.WaitAsync(token);
            Check(Environment.CurrentManagedThreadId == uiThread, "Async UI actions resume on their creating UI thread");
        }); Mount(button);
        var pending = button.ExecuteAsync();
        Check(button.AsyncCommand.IsRunning && !button.Enabled && button.Text == button.BusyText, $"The running command disables the native button and shows busy text: running={button.AsyncCommand.IsRunning}, enabled={button.Enabled}, text={button.Text}");
        Check(!Complete(button.ExecuteAsync()) && executions == 1, "Duplicate requests do not execute twice");
        gate.SetResult(); Check(Complete(pending) && button.Enabled && button.Text == "登録", $"Completion restores the button: enabled={button.Enabled}, text={button.Text}");
        Check(Complete(Task.Run(() => button.ExecuteAsync())) && executions == 2, "Requests from another thread are marshaled to the UI thread");
        using (var canceledSource = new CancellationTokenSource())
        {
            canceledSource.Cancel();
            Check(Canceled(button.ExecuteAsync(canceledSource.Token)) && executions == 2, "An already canceled caller token prevents the action from starting");
        }

        var cancel = Elements.ButtonAsync("取消可能", token => Task.Delay(Timeout.Infinite, token)); Mount(cancel);
        var canceled = cancel.ExecuteAsync(); cancel.Cancel();
        Check(Canceled(canceled) && cancel.AsyncCommand.IsCanceled && cancel.AsyncCommand.LastError is null && cancel.Enabled,
            "Cancellation is observable without being reported as a failure");
        var marker = new InvalidOperationException("command marker"); int failures = 0; bool fail = true;
        var failing = Elements.ButtonAsync("失敗", _ => fail ? Task.FromException(marker) : Task.CompletedTask); Mount(failing);
        failing.ExecutionFailed += (_, e) => { Check(ReferenceEquals(e.Exception, marker), "Failure retains the original exception"); failures++; };
        failing.PerformClick();
        Check(failures == 1 && ReferenceEquals(failing.AsyncCommand.LastError, marker) && failing.Enabled, "Native ICommand clicks publish failures without an unhandled async exception");
        fail = false; Check(Complete(failing.ExecuteAsync()) && failing.AsyncCommand.LastError is null, "Retry clears the previous failure");
        fail = true;
        bool propagated = false;
        try { Complete(failing.ExecuteAsync()); } catch (InvalidOperationException error) { propagated = ReferenceEquals(error, marker); }
        Check(propagated && failures == 2, "Awaiters receive command failures");

        bool allowed = false; object? received = null;
        using var command = new AsyncCommand((parameter, _) => { received = parameter; return Task.CompletedTask; }, _ => allowed);
        var native = Elements.Button("共有コマンド", command, "parameter"); Mount(native);
        Check(!native.Enabled, "Native command binding honors CanExecute");
        allowed = true; command.NotifyCanExecuteChanged(); native.PerformClick();
        Check(native.Enabled && Equals(received, "parameter"), "Native command binding passes its parameter and reacts to eligibility changes");
        var disposed = Elements.ButtonAsync("終了", token => Task.Delay(Timeout.Infinite, token)); Mount(disposed);
        var disposing = disposed.ExecuteAsync(); disposed.Dispose();
        Check(Canceled(disposing) && !Complete(disposed.AsyncCommand.ExecuteAsync())
            && !Complete(Task.Run(() => disposed.ExecuteAsync())), "Disposing an async button cancels its owned command and rejects UI or background requests");
        results.Add("PASS: Async buttons/native ICommand: busy state, eligibility, parameters, duplicate prevention, cancellation, failure observation, retry and disposal");

        var model = new InputModel { Name = "initial", Quantity = 2 };
        var editor = new ModelForm<InputModel>(model) { ValidateOnChange = true }; Mount(editor);
        int values = 0, dirtyChanges = 0, validationChanges = 0;
        string? changedProperty = null;
        editor.ValueChanged += (_, e) => { values++; changedProperty = e.PropertyName; };
        editor.IsDirtyChanged += (_, _) => dirtyChanges++;
        editor.ValidationChanged += (_, _) => validationChanges++;
        var name = (InputField<object?>)editor.Fields[nameof(InputModel.Name)];
        var quantity = (InputField<object?>)editor.Fields[nameof(InputModel.Quantity)];
        ((TextBox)name.Editor).Text = "";
        Check(values == 1 && changedProperty == nameof(InputModel.Name) && dirtyChanges == 1 && editor.IsDirty
            && editor.HasErrors && editor.IsValidated && editor.ErrorsByField.ContainsKey(nameof(InputModel.Name)), "A native input change publishes typed dirty/validation state and property-name errors");
        Check(editor.FocusFirstError() && name.Editor.Focused, "The first invalid editor can be focused");
        ((TextBox)quantity.Editor).Text = "invalid";
        Check(editor.ErrorsByField.ContainsKey(nameof(InputModel.Quantity)) && !editor.TryRead(out _), "Invalid conversions remain observable instead of escaping the notification handler");
        ((TextBox)name.Editor).Text = "edited"; ((TextBox)quantity.Editor).Text = "5";
        Check(!editor.HasErrors && editor.TryRead(out var read) && read!.Name == "edited" && model.Name == "initial", "Fixing values clears validation and preserves the source model");
        Check(editor.AcceptChanges() && !editor.IsDirty && dirtyChanges == 2, "Successful saves can establish a new baseline");
        ((TextBox)name.Editor).Text = "later"; int beforeReset = values;
        editor.Reset();
        Check(!editor.IsDirty && !editor.HasErrors && !editor.IsValidated && (string?)name.Value == "edited"
            && values == beforeReset + 1 && changedProperty is null, "Reset publishes one coherent whole-form change");
        editor.LoadModel(new InputModel { Name = "loaded", Quantity = 8 });
        Check(!editor.IsDirty && changedProperty is null && (string?)name.Value == "loaded", "LoadModel publishes a new clean baseline");
        editor.ValidateOnChange = false; ((TextBox)name.Editor).Text = "";
        Check(!editor.IsValidated && !editor.HasErrors && !editor.TryRead(out _), "Validation stays explicit unless ValidateOnChange is enabled");
        editor.ClearValidation(); Check(!editor.HasErrors && !editor.IsValidated && validationChanges > 0, "Clearing validation publishes the unknown validation state");
        var checkField = (InputField<object?>)editor.Fields[nameof(InputModel.Flag)];
        int checks = 0; checkField.ValueChanged += (_, _) => checks++;
        ((CheckBox)checkField.Editor).CheckState = CheckState.Checked;
        ((CheckBox)checkField.Editor).CheckState = CheckState.Indeterminate;
        Check(checks == 2 && checkField.Value is null, "Nullable checkbox transitions are notified once each");
        Check(name.Name.Length == 0 && name.Editor.Name == nameof(InputModel.Name) && name.LabelText == "名前",
            "Only model input editors are named from their property; display labels and wrappers need no identifiers");
        results.Add("PASS: Form notifications: native editor changes, dirty transitions, conversion/annotation errors, field keys, first-error focus, live validation, acceptance, reset/load batching and nullable checks");
        host.Close();
    }

    private static T Complete<T>(Task<T> task)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted) { Application.DoEvents(); if (timer.ElapsedMilliseconds > 5000) throw new TimeoutException("Interaction verification timed out"); Thread.Sleep(5); }
        return task.GetAwaiter().GetResult();
    }
    private static bool Canceled(Task<bool> task) { try { Complete(task); return false; } catch (OperationCanceledException) { return true; } }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class InputModel
    {
        [Required, DisplayName("名前")] public string Name { get; set; } = "";
        [Range(0, 10)] public int Quantity { get; set; }
        public bool? Flag { get; set; }
    }
}
