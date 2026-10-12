namespace Tansu.Forms.Sample;

internal static class StartupVerification
{
    internal static void Run(ICollection<string> results)
    {
        Check(Thread.CurrentThread.GetApartmentState() == ApartmentState.STA && Application.OleRequired() == ApartmentState.STA,
            "Top-level sample startup initializes the UI and OLE on STA");
        int originalThread = Environment.CurrentManagedThreadId;
        bool reused = false;
        FormsApp.Start(() => reused = Environment.CurrentManagedThreadId == originalThread);
        Check(reused, "An STA caller stays on the same thread");
        bool shownOnSta = false, callbackOnSta = false, nestedRejected = false, closed = false, exceptionPreserved = false;
        Exception? unexpected = null;
        var marker = new InvalidOperationException("factory marker");
        var caller = new Thread(() =>
        {
            try
            {
                int callerId = Environment.CurrentManagedThreadId;
                FormsApp.Run(form =>
                {
                    callbackOnSta = Thread.CurrentThread.GetApartmentState() == ApartmentState.STA && Environment.CurrentManagedThreadId != callerId;
                    form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-10000, -10000);
                    form.Shown += (_, _) => form.BeginInvoke((Action)(() =>
                    {
                        shownOnSta = Thread.CurrentThread.GetApartmentState() == ApartmentState.STA && Application.MessageLoop;
                        try { FormsApp.Run(() => new Form()); }
                        catch (InvalidOperationException) { nestedRejected = true; }
                        form.Close();
                    }));
                });
                closed = true;
                try { FormsApp.Run((Func<Form>)(() => throw marker)); }
                catch (InvalidOperationException error) { exceptionPreserved = ReferenceEquals(error, marker) && error.StackTrace?.Contains(nameof(StartupVerification)) == true; }
            }
            catch (Exception error) { unexpected = error; }
        }) { IsBackground = true };
        caller.SetApartmentState(ApartmentState.MTA); caller.Start();
        Check(caller.Join(10000), "MTA startup closes its message loop and returns");
        if (unexpected is not null) throw new InvalidOperationException("Startup verification failed", unexpected);
        Check(callbackOnSta && shownOnSta && closed && nestedRejected && exceptionPreserved,
            "MTA startup creates and runs controls on STA, returns after close, rejects nested loops and preserves startup exceptions");
        results.Add("PASS: Main/STAThread-free startup, STA reuse, MTA-to-STA form construction/message loop/OLE, close, nested-loop guard and exception propagation");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
