#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:property OutputType=Exe
#:property DisableWinExeOutputInference=true
#:property PublishAot=false
#:project ../../src/Tansu.Forms.Components/Tansu.Forms.Components.csproj

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Tansu.Forms.Components;
using Tansu.Forms.Execution;
using Tansu.Forms.Layout;
using UiLayout = Tansu.Forms.Layout.Layout;

return await ActionApp.RunAsync(args, PageAction.Insert<SiteRegistrationInput>(Insert), form =>
{
    form.Text = "設備のIPアドレス・拠点登録";
    var editor = new ModelForm<SiteRegistrationInput>(new() { IpAddress = "192.168.1.10", SiteKey = "tokyo" });
    var status = Elements.Label("IPアドレスと拠点を選択して登録してください。");
    var buttons = UiLayout.Row(form.InsertButton(editor, status: status));
    buttons.FlowDirection = FlowDirection.RightToLeft;
    UiLayout.Root(form, RootSize.Small, editor, buttons, status);
});

static ActionResponse Insert(SiteRegistrationInput input)
{
    // 実際のDB・ファイルへの登録処理をここに追加してください。
    return new(201, input, $"登録しました。（{SiteOptions.Values[input.SiteKey]} / {input.IpAddress}）");
}

public sealed class SiteRegistrationInput
{
    [DisplayName("IPアドレス"), Required, IpAddress(IpAddressKind.IPv4)]
    public string IpAddress { get; set; } = "";

    [DisplayName("拠点"), Required, Choices(typeof(SiteOptions), nameof(SiteOptions.Values))]
    public string SiteKey { get; set; } = "";
}

public static class SiteOptions
{
    public static IReadOnlyDictionary<string, string> Values { get; } = new Dictionary<string, string>
    {
        ["tokyo"] = "東京拠点", ["osaka"] = "大阪拠点", ["nagoya"] = "名古屋拠点"
    };
}
