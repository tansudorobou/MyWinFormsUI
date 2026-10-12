using Tansu.Forms.Execution;

namespace Tansu.Forms.Components;

/// <summary>No arguments opens the UI; arguments execute the same action through JSON stdin/stdout without constructing controls.</summary>
public static class ActionApp
{
    public static async Task<int> RunAsync(string[] args, IPageAction action, System.Action<ActionForm> configure,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args); ArgumentNullException.ThrowIfNull(action); ArgumentNullException.ThrowIfNull(configure);
        if (args.Length > 0) return await new ToolRunner(action).RunAsync(args, cancellationToken: cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        FormsApp.Run(() =>
        {
            var form = new ActionForm(action);
            try { configure(form); return form; }
            catch { form.Dispose(); throw; }
        });
        return 0;
    }
}
