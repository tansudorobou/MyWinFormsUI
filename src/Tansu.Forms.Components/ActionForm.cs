using Tansu.Forms.Execution;

namespace Tansu.Forms.Components;

/// <summary>A native Form with exactly one page action. Owns the client, not the supplied handler.</summary>
public class ActionForm : Form
{
    private readonly int _uiThread = Environment.CurrentManagedThreadId;
    public ActionForm(IPageAction action)
    {
        Action = new ActionClient(action);
        Action.StateChanged += OnActionStateChanged;
        Text = "Tansu.Forms";
        ClientSize = RootSizes.GetSize(RootSize.Standard);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
    }
    public ActionClient Action { get; }
    /// <summary>Marshaled to the form's UI thread. Controls stay alive across requests.</summary>
    public event EventHandler? ActionStateChanged;
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ActionStateChanged?.Invoke(this, EventArgs.Empty);
    }
    private void OnActionStateChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || Disposing) return;
        if (Environment.CurrentManagedThreadId == _uiThread) ActionStateChanged?.Invoke(this, EventArgs.Empty);
        else if (IsHandleCreated)
        {
            try { BeginInvoke((System.Action)(() => { if (!IsDisposed && !Disposing) ActionStateChanged?.Invoke(this, EventArgs.Empty); })); }
            catch (InvalidOperationException) when (IsDisposed || Disposing || !IsHandleCreated) { }
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { Action.StateChanged -= OnActionStateChanged; Action.Dispose(); }
        base.Dispose(disposing);
    }
}
