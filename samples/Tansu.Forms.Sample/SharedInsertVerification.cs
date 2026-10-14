using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Tansu.Forms.Execution;

namespace Tansu.Forms.Sample;

internal static class SharedInsertVerification
{
    internal static void Run(ICollection<string> results)
    {
        int inserts = 0;
        var action = PageAction.Insert<Input>(input => { inserts++; return new(201, input, "登録しました。"); });
        using var form = new ActionForm(action) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000) };
        var editor = new ModelForm<Input>(); var status = Elements.Label("");
        var button = form.InsertButton(editor, status: status);
        form.Controls.Add(Tansu.Forms.Layout.Layout.Stack(editor, button, status)); form.Show(); Application.DoEvents();
        var ip = (InputField<object?>)editor.Fields[nameof(Input.Ip)];
        var site = (InputField<object?>)editor.Fields[nameof(Input.Site)];
        Check(ip.Editor.Name == nameof(Input.Ip) && site.Editor is ComboBox, "Attributes generate named text and ComboBox editors");
        ip.Value = "127.1"; Complete(button.ExecuteAsync());
        Check(inserts == 0 && editor.ErrorsByField.ContainsKey(nameof(Input.Ip)), "Form uses shared IP validation before dispatch");
        var invalidIp = Complete(form.Action.InsertAsync(new Input { Ip = "127.1" }));
        Check(invalidIp.StatusCode == 422 && invalidIp.FieldErrors.ContainsKey(nameof(Input.Ip)) && inserts == 0, "Action uses the same IP rule without UI");
        var invalidSite = Complete(form.Action.InsertAsync(new Input { Site = "missing" }));
        Check(invalidSite.StatusCode == 422 && invalidSite.FieldErrors.ContainsKey(nameof(Input.Site)), "Choice keys are validated by the action");
        ip.Value = "192.168.1.20"; site.Value = "osaka"; Complete(button.ExecuteAsync());
        Check(inserts == 1 && form.Action.LastResponse?.ReadBody<Input>().Site == "osaka" && status.Text == "登録しました。"
            && editor.Enabled && !editor.IsDirty, "InsertButton reads the model, dispatches Insert, displays success and accepts changes");
        Check(Complete(form.Action.SelectAsync()).StatusCode == 405, "Insert-only adapter rejects unsupported methods");
        Check(Complete(form.Action.InsertAsync(new Input(), path: "/other")).StatusCode == 404, "Insert adapter checks the path");
        form.Close();
        results.Add("PASS: Shared model IP/choice validation in GUI/action, automatic names and ComboBox, Insert-only delegate adapter and compact registration button");

        int registered = 0;
        ActionResponse Register(Input input) { registered++; return new(200, input, "任意関数を実行しました。"); }
        using var registration = PageAction.Create<Input>(Register);
        using var registeredForm = new ActionForm(registration) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000) };
        var registeredEditor = new ModelForm<Input>(); var registeredStatus = Elements.Label("");
        var registeredButton = registeredForm.ExecuteButton(registeredEditor, registration, text: "登録", status: registeredStatus);
        registeredForm.Controls.Add(Tansu.Forms.Layout.Layout.Stack(registeredEditor, registeredButton, registeredStatus));
        registeredForm.Show(); Application.DoEvents();
        var registeredIp = (InputField<object?>)registeredEditor.Fields[nameof(Input.Ip)];
        registeredIp.Value = "invalid"; Complete(registeredButton.ExecuteAsync());
        Check(registered == 0 && registeredEditor.HasErrors, "ExecuteButton validates the editor before notifying the subscribed function");
        registeredIp.Value = "192.168.1.30"; Complete(registeredButton.ExecuteAsync());
        Check(registered == 1 && registeredStatus.Text == "任意関数を実行しました。" && !registeredEditor.IsDirty
            && registeredEditor.Enabled && registeredForm.Action.LastResponse?.ReadBody<Input>().Ip == "192.168.1.30",
            "ExecuteButton executes a subscription without operation names and retains status, input locking and saved state");
        registeredForm.Close();
        results.Add("PASS: Compact ExecuteButton runs arbitrary subscribed functions with shared validation and status");
    }
    private static T Complete<T>(Task<T> task)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted) { Application.DoEvents(); if (timer.ElapsedMilliseconds > 5000) throw new TimeoutException(); Thread.Sleep(5); }
        return task.GetAwaiter().GetResult();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    public sealed class Input
    {
        [DisplayName("IP"), Required, IpAddress(IpAddressKind.IPv4)] public string Ip { get; set; } = "192.168.1.10";
        [DisplayName("拠点"), Required, Choices(typeof(Options), nameof(Options.Values))] public string Site { get; set; } = "tokyo";
    }
    public static class Options
    {
        public static IReadOnlyDictionary<string, string> Values { get; } = new Dictionary<string, string> { ["tokyo"] = "東京", ["osaka"] = "大阪" };
    }
}
