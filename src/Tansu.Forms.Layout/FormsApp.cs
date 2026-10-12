using System.Runtime.ExceptionServices;

namespace Tansu.Forms.Layout;

/// <summary>WinForms startup for top-level programs. Construct all UI controls inside the callback.</summary>
public static class FormsApp
{
    private static readonly object InitializationGate = new();
    private static bool _initialized;

    /// <summary>Create and configure a plain Form on the STA UI thread, then run its message loop.</summary>
    public static void Run(Action<Form> configure, Action? initialize = null)
    {
        ArgumentNullException.ThrowIfNull(configure);
        Run(() =>
        {
            var form = new Form
            {
                Text = "Tansu.Forms", ClientSize = RootSizes.GetSize(RootSize.Standard),
                StartPosition = FormStartPosition.CenterScreen, AutoScaleMode = AutoScaleMode.Dpi
            };
            try { configure(form); return form; }
            catch { form.Dispose(); throw; }
        }, initialize);
    }

    /// <summary>Create an existing Form subclass on the STA UI thread and run until it closes.</summary>
    public static void Run(Func<Form> createForm, Action? initialize = null)
    {
        ArgumentNullException.ThrowIfNull(createForm);
        Start(() =>
        {
            using var form = createForm() ?? throw new InvalidOperationException("起動するFormを返してください。");
            if (form.IsHandleCreated && form.InvokeRequired)
                throw new InvalidOperationException("FormはFormsApp.Runのコールバック内で作成してください。");
            Application.Run(form);
        }, initialize);
    }

    /// <summary>Initialize WinForms and execute a custom startup action on STA, waiting until it completes.</summary>
    public static void Start(Action action, Action? initialize = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            Execute(action, initialize); return;
        }
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { Execute(action, initialize); }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        }) { Name = "Tansu.Forms UI" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        failure?.Throw();
    }

    private static void Execute(Action action, Action? initialize)
    {
        if (Application.MessageLoop)
            throw new InvalidOperationException("FormsAppはアプリの起動時に使用してください。開いている画面からはForm.Showを使用してください。");
        lock (InitializationGate)
        {
            if (!_initialized)
            {
                if (initialize is not null) initialize();
                else
                {
                    Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                }
                _initialized = true;
            }
        }
        action();
    }
}
