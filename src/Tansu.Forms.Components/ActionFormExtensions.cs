namespace Tansu.Forms.Components;

public static class ActionFormExtensions
{
    /// <summary>Typed insert with shared annotations, input locking, action errors and result display.</summary>
    public static AsyncButton InsertButton<T>(this ActionForm form, ModelForm<T> editor,
        string text = "登録", Label? status = null, string path = "/") where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(form); ArgumentNullException.ThrowIfNull(editor);
        void Show(string message) { if (status is { IsDisposed: false }) status.Text = message; }
        var button = Elements.ButtonAsync(text, async token =>
        {
            if (!editor.TryRead(out var input)) { editor.FocusFirstError(); Show("入力内容を確認してください。"); return; }
            editor.Enabled = false;
            try
            {
                var response = await form.Action.InsertAsync(input, path, cancellationToken: token);
                if (form.IsDisposed || form.Disposing || editor.IsDisposed) return;
                if (response.IsSuccess) { editor.AcceptChanges(); Show(response.Message ?? "登録しました。"); }
                else { editor.ApplyErrors(response.FieldErrors, response.Message); Show(response.Message ?? "登録に失敗しました。"); }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { Show("登録を取り消しました。"); throw; }
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
