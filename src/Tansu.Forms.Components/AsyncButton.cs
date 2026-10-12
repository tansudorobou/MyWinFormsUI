using System.ComponentModel;

namespace Tansu.Forms.Components;

/// <summary>A native command button which owns its async command and cancels it on disposal.</summary>
public sealed class AsyncButton : Button
{
    private readonly ToolTip _errorTip = new();
    private string _idleText = "";
    private bool _settingText;

    internal AsyncButton(string text, Func<CancellationToken, Task> execute, Func<bool>? canExecute)
    {
        Text = text;
        AsyncCommand = new AsyncCommand(execute, canExecute);
        Command = AsyncCommand;
        AsyncCommand.StateChanged += CommandStateChanged;
        AsyncCommand.ExecutionFailed += CommandFailed;
    }

    public AsyncCommand AsyncCommand { get; }
    [DefaultValue("処理中…")]
    public string BusyText { get; set; } = "処理中…";
    public event EventHandler<CommandFailedEventArgs>? ExecutionFailed;
    public Task<bool> ExecuteAsync(CancellationToken cancellationToken = default) => AsyncCommand.ExecuteAsync(cancellationToken: cancellationToken);
    public void Cancel() => AsyncCommand.Cancel();

    protected override void OnTextChanged(EventArgs e)
    {
        if (!_settingText) _idleText = Text;
        base.OnTextChanged(e);
    }

    private void CommandStateChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || Disposing) return;
        _settingText = true;
        try { Text = AsyncCommand.IsRunning ? BusyText : _idleText; }
        finally { _settingText = false; }
        _errorTip.SetToolTip(this, AsyncCommand.LastError?.Message);
        this.NotifyLayoutChanged();
    }

    private void CommandFailed(object? sender, CommandFailedEventArgs e) => ExecutionFailed?.Invoke(this, e);
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AsyncCommand.StateChanged -= CommandStateChanged;
            AsyncCommand.ExecutionFailed -= CommandFailed;
            Command = null;
            AsyncCommand.Dispose(); _errorTip.Dispose();
        }
        base.Dispose(disposing);
    }
}
