using Tansu.Forms.Execution;

namespace Tansu.Forms.Components;

public static class ActionFormExtensions
{
    /// <summary>Typed insert with shared annotations, input locking, action errors and result display.</summary>
    public static AsyncButton InsertButton<T>(this ActionForm form, ModelForm<T> editor,
        string text = "登録", Label? status = null, string path = "/") where T : class, new()
        => CreateButton(form, editor, (input, token) => form.Action.InsertAsync(input, path, cancellationToken: token),
            text, status, "登録しました。", "登録に失敗しました。", "登録を取り消しました。");

    /// <summary>Execute a subscribed function with shared validation, input locking, errors and status display.</summary>
    public static AsyncButton ExecuteButton<T>(this ActionForm form, ModelForm<T> editor, ActionSubscription<T> function,
        string text = "実行", Label? status = null) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(function);
        return CreateButton(form, editor, (input, token) => form.Action.ExecuteAsync(function, input, token),
            text, status, "実行しました。", "実行に失敗しました。", "実行を取り消しました。");
    }

    private static AsyncButton CreateButton<T>(ActionForm form, ModelForm<T> editor,
        Func<T, CancellationToken, Task<ActionResponse>> execute, string text, Label? status,
        string success, string failure, string canceled) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(form); ArgumentNullException.ThrowIfNull(editor);
        void Show(string message) { if (status is { IsDisposed: false }) status.Text = message; }
        var button = Elements.ButtonAsync(text, async token =>
        {
            if (!editor.TryRead(out var input)) { editor.FocusFirstError(); Show("入力内容を確認してください。"); return; }
            editor.Enabled = false;
            try
            {
                var response = await execute(input!, token);
                if (form.IsDisposed || form.Disposing || editor.IsDisposed) return;
                if (response.IsSuccess) { editor.AcceptChanges(); Show(response.Message ?? success); }
                else { editor.ApplyErrors(response.FieldErrors, response.Message); Show(response.Message ?? failure); }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { Show(canceled); throw; }
            finally { if (!editor.IsDisposed) editor.Enabled = true; }
        }, () => !form.IsDisposed && !form.Disposing && !form.Action.IsRunning && !editor.IsDisposed && editor.Enabled);
        button.ExecutionFailed += (_, e) => Show(e.Exception.Message);
        void Changed(object? sender, EventArgs e) { if (!button.IsDisposed && !button.Disposing) button.AsyncCommand.NotifyCanExecuteChanged(); }
        form.ActionStateChanged += Changed;
        editor.EnabledChanged += Changed;
        button.Disposed += (_, _) => { form.ActionStateChanged -= Changed; editor.EnabledChanged -= Changed; };
        return button;
    }
}
