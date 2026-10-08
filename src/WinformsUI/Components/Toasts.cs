namespace WinformsUI;

public enum ToastKind { Information, Success, Warning, Error, Loading }

public sealed class ToastNotification : UserControl
{
    private readonly Label _message = new() { AutoSize = false, AccessibleRole = AccessibleRole.Alert };
    private readonly Label _kind = new() { AutoSize = true };
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly StackPanel _content;
    public Button CloseButton { get; }
    public Button? ActionButton { get; }
    public ToastKind Kind { get; private set; }
    public string MessageText => _message.Text;
    public event EventHandler? Dismissed;

    internal ToastNotification(string message, ToastKind kind, Action? action, string actionText)
    {
        CloseButton = Ui.Button("閉じる", Dismiss);
        if (action is not null) ActionButton = Ui.Button(actionText, () => { action(); Dismiss(); });
        _content = WinformsUI.Layout.Stack(_kind, _message,
            ActionButton is null ? WinformsUI.Layout.Row(CloseButton) : WinformsUI.Layout.Row(ActionButton, CloseButton));
        _content.Dock = DockStyle.Fill; _content.Gap = 4;
        Controls.Add(_content); Margin = Padding.Empty;
        _timer.Tick += (_, _) => Dismiss();
        Update(message, kind);
    }
    public void Update(string message, ToastKind kind, int durationMilliseconds = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(durationMilliseconds);
        _timer.Stop(); Kind = kind; _message.Text = message;
        _kind.Text = kind switch { ToastKind.Success => "完了", ToastKind.Error => "エラー", ToastKind.Warning => "注意", ToastKind.Loading => "処理中", _ => "お知らせ" };
        _message.WithRole(kind == ToastKind.Error ? "danger" : "label");
        if (durationMilliseconds > 0) { _timer.Interval = durationMilliseconds; _timer.Start(); }
        LayoutInvalidation.NotifyAncestors(this);
    }
    public void Dismiss()
    {
        if (IsDisposed) return;
        _timer.Stop(); Dismissed?.Invoke(this, EventArgs.Empty); Dispose();
    }
    public override Size GetPreferredSize(Size proposedSize) => _content is null ? base.GetPreferredSize(proposedSize) : _content.GetPreferredSize(proposedSize);
    protected override void Dispose(bool disposing) { if (disposing) _timer.Dispose(); base.Dispose(disposing); }
}

/// <summary>Local, dismissible/actionable notifications with optional async work tracking.</summary>
public sealed class ToastHost : UserControl
{
    private readonly StackPanel _stack = WinformsUI.Layout.Stack();
    public ToastHost() { _stack.Dock = DockStyle.Fill; Controls.Add(_stack); Margin = Padding.Empty; }
    public IReadOnlyList<ToastNotification> Notifications => _stack.Controls.OfType<ToastNotification>().ToArray();
    public ToastNotification Notify(string message, ToastKind kind = ToastKind.Information, int durationMilliseconds = 4000,
        Action? action = null, string actionText = "実行")
    {
        ArgumentOutOfRangeException.ThrowIfNegative(durationMilliseconds);
        var toast = new ToastNotification(message, kind, action, actionText);
        _stack.Add(toast); toast.Update(message, kind, durationMilliseconds); return toast;
    }
    public async Task<ToastNotification> TrackAsync(Func<Task> work, string loading, string success, string errorPrefix = "処理に失敗しました")
    {
        ArgumentNullException.ThrowIfNull(work);
        var toast = Notify(loading, ToastKind.Loading, 0);
        try { await work(); if (!toast.IsDisposed) toast.Update(success, ToastKind.Success, 4000); }
        catch (Exception error) { if (!toast.IsDisposed) toast.Update($"{errorPrefix}: {error.Message}", ToastKind.Error, 0); }
        return toast;
    }
    public override Size GetPreferredSize(Size proposedSize) => _stack is null ? base.GetPreferredSize(proposedSize) : _stack.GetPreferredSize(proposedSize);
}
