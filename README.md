# Tansu.Forms

Windows Formsの標準コントロールを、コードだけで組み合わせるUIライブラリです。
入力欄の上にラベルを表示し、項目数とウィンドウ幅に合わせて配置します。
デザイナーや `.Designer.cs`、外部UIパッケージは使用しません。

## プロジェクト

| プロジェクト | 内容 |
| --- | --- |
| `src/Tansu.Forms.Layout` | 自動配置、ルートサイズ・最小サイズ、STA設定を任せるFormsApp起動API |
| `src/Tansu.Forms.Components` | 入力部品、データグリッド、検索、ガント、テーマ |
| `src/Tansu.Forms.Execution` | WinForms非依存の関数登録・実行、Request/Response、関数一覧付きJSON CLI入出力（net10.0） |
| `samples/Tansu.Forms.Sample` | 製造実績の登録・検索・ソートと自動配置を試せるアプリ |
| `tests/Tansu.Forms.UiTests` | FlaUI/UI Automationによる別プロセスからの入力・ボタン操作の検証 |
| `tests/Tansu.Forms.Execution.Tests` | GUIなしで実行するAction・CLI入出力の検証 |

必要な環境は **Windowsと.NET 10 SDK** です。ソリューションは `Tansu.Forms.slnx` です。

依存方向は `Sample → Components → Layout / Execution` です。サンプルはLayoutも直接参照しています。
ExecutionはWinFormsやPowerShellに依存せず、画面のないツールでも単独で使えます。
LayoutはComponentsを参照せず、標準のLabelやTextBoxだけを並べるアプリにも単独で使えます。
テーマからレイアウトの余白を変更する処理はComponents側に置いています。
名前空間はそれぞれ `Tansu.Forms.Layout` / `Tansu.Forms.Components` / `Tansu.Forms.Execution` / `Tansu.Forms.Sample` です。

旧構成からはプロジェクト参照と `using WinformsUI;` を変更してください。
`Ui.Button` / `Ui.Label` は `Elements.Button` / `Elements.Label` に変更しました。
データ・テーマJSON・その他の部品APIと操作はそのままです。

shadcn/uiの公式一覧64項目に対応する機能を、[コンポーネント対応表](docs/COMPONENTS.md)に整理しています。
25項目は標準部品にない動作を合成部品で補い、39項目はWindows Forms標準のAPIを使用します。
WebのDOM/SSRやReact/Tailwindの実装方式は.NETへ移植せず、UIの操作・データ・状態の機能を対象としています。

```powershell
dotnet build Tansu.Forms.slnx
dotnet run --project samples/Tansu.Forms.Sample
```

全体の部品ギャラリーは、実績サンプルの「コンポーネント一覧」ボタン、または次のコマンドで開けます。

```powershell
dotnet run --project samples/Tansu.Forms.Sample -- --gallery
```

## Program.csだけで起動する

トップレベルの `Program.cs` に次のように書けます。`Program` クラス、`Main`、`[STAThread]`、`Application.Run` の宣言は不要です。

```csharp
using Tansu.Forms.Components;
using Tansu.Forms.Layout;
using FormLayout = Tansu.Forms.Layout.Layout;

FormsApp.Run(form =>
{
    form.Text = "実績入力";
    var part = Field.Text("品番");
    FormLayout.Root(form, RootSize.Standard,
        part, Elements.Button("確認", () => MessageBox.Show(form, part.Value)));
});
```

通常の `.csproj` では `net10.0-windows` / `UseWindowsForms=true` / `OutputType=WinExe` を設定し、ライブラリを参照します。
既存のFormクラスを使う場合も `FormsApp.Run(() => new MyForm());` の1行で起動できます。
`FormsApp` がWinFormsの初期化、STAスレッド、メッセージループ、終了までの待機を引き受けます。
MTAから呼んだ場合は専用のSTAスレッドを作り、既にSTAならそのスレッドを使用します。
Formも入力部品もコールバック内で作成してください。画面の終了後に呼び出し元へ戻り、構築時の例外は元のスタック情報を保持して呼び出し元へ返します。
開いている画面から追加画面を表示する場合は通常の `Show` / `ShowDialog` を使います。

既定の初期化は `PerMonitorV2`、Visual Styles有効、互換テキスト描画無効です。初期化はプロセス内で1回だけ実行します。
プロジェクトの生成設定を使う場合は `FormsApp.Run(() => new MyForm(), initialize: () => ApplicationConfiguration.Initialize());` と指定できます。
独自の起動処理や検証は `FormsApp.Start(() => { /* STA上の処理 */ });` で実行できます。

### .csprojを作らずに実行する

.NET 10の [file-based apps](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps) に対応した [単一ファイルのサンプル](samples/Tansu.Forms.SingleFile/Program.cs) を用意しています。
入力、Listモデルの表、検索、登録を `Program.cs` にまとめ、クラスの `DisplayName` で列名を設定しています。

```powershell
dotnet run --file samples/Tansu.Forms.SingleFile/Program.cs
```

このサンプルの先頭には次の設定を記述しています。

```csharp
#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:property OutputType=WinExe
#:property PublishAot=false
#:project ../../src/Tansu.Forms.Components/Tansu.Forms.Components.csproj
```

`#:project` はこのファイルからの相対パスです。別の場所にコピーする場合は参照先を変更してください。
`PublishAot=false` は通常のWinFormsとして発行するための指定です。
単一ファイルの動作検証は同じコマンドに `-- --verify` を付けると自動終了します。

サンプルでは「項目を追加」「追加項目を削除」で動的な配置を確認できます。
ウィンドウを狭くすると入力欄が少ない列に並び替わり、高さが足りない場合は縦にスクロールします。
追加項目は配置の確認用で、実績の保存対象には含みません。
実績はメモリ内の `List<ProductionRecord>` に保存され、終了すると消えます。
データクラスは `ProductionRecord.cs`、初期データの作成は `SampleData.cs` にまとめています。

## 1ファイルでGUIとCLIから同じActionを実行する

`PageAction.Create<T>(関数)` の1行で任意の関数を登録し、GUIとCLIから共用できます。
SQL操作名の指定、Actionクラス、`switch` による振り分け、関数名の文字列の重複記述は不要です。

```csharp
using var save = PageAction.Create<MyInput>(Save);
return await ActionApp.RunAsync(args, save, form =>
{
    var editor = new ModelForm<MyInput>();
    var status = Elements.Label("");
    Layout.Root(form, RootSize.Small, editor,
        form.ExecuteButton(editor, save, text: "保存", status: status), status);
});

static ActionResponse Save(MyInput input) => new(200, input, "保存しました。");
```

`Create<T>` は入力モデルを受け取る同期関数・非同期関数を登録し、入力なしなら `PageAction.Create(Ping)` のように書けます。
返り値をそのまま `ActionApp` / `ActionClient` / `ToolRunner` とGUIのボタンに渡せるため、レジストリーの生成と関数登録を分けて書く必要はありません。
ファクトリーは呼び出すたびに独立した登録を作成します。
非同期は `Task<ActionResponse> SaveAsync(MyInput input, CancellationToken token)` と、トークンなしの形に対応します。
GUIでは `form.Action.ExecuteAsync(save, input)` または `form.ExecuteButton(editor, save)` に登録オブジェクトを渡します。
CLIでは `Save --body ...` のように関数名を指定します。通常のメソッドもトップレベルのローカル関数も名前を自動取得します。
ラムダを登録するときやCLIの名前を変えるときは `name: "Save"`、ヘルプの説明には `description: "入力を保存"` を指定できます。
複数の関数は `var actions = PageAction.Create();` にそれぞれ `actions.Subscribe<T>(関数)` で追加し、`actions` を起動APIに渡します。
`--help` / `-h` は現在登録されている関数名・入力項目の順序・説明を表示します。ヘルプ表示では関数やGUIを実行しません。

登録・解除を行うオブザーバー形式のAPIです。返された登録オブジェクトの `Dispose()` で解除します。
GUI/CLIの終了まで登録を保持してください。実行済みの処理は登録解除で取り消されず、取消には `ActionClient.Cancel()` を使用します。
1つの関数名につき1つの処理を登録でき、同名の重複登録（大文字・小文字違いを含む）はエラーになります。
実行は関数名で振り分けます。`Path` / `Parameters` の解釈が必要な独自処理は `IPageAction` で実装できます。

[SiteRegistration.cs](samples/Tansu.Forms.SingleFile/SiteRegistration.cs)は、IPアドレスと拠点を入力して登録する別のサンプルです。
`Program.cs`からは独立して実行します。

```powershell
dotnet run --file samples/Tansu.Forms.SingleFile/SiteRegistration.cs
```

IPv4の必須入力と、初期設定済みComboBoxによる東京・大阪・名古屋の拠点選択があります。
サンプルでは `PageAction.Create<SiteRegistrationInput>(Register)` と `form.ExecuteButton(editor, register, text: "登録", status: status)` を使用します。
Actionクラス・振り分け処理・DTOの手動作成・項目名の手動設定・GUIとActionの重複検証は不要です。
`ModelForm<T>` が `[DisplayName]` からラベルと項目名を設定し、`[Choices]` の辞書からComboBoxを生成します。
`[Required]` / `[IpAddress(IpAddressKind.IPv4)]` / `[Choices]` はDataAnnotationsとしてフォームとActionで共用します。
`ExecuteButton` は入力検証、実行中の入力ロック、エラー表示、成功メッセージ、変更の保存済み扱いをまとめます。
Actionの `ReadBody<T>` も同じ属性で検証するので、GUIを通らない不正入力も登録関数へ渡りません。
成功すると画面に「登録しました。」と選択した拠点・IPを表示します。
このサンプルのRegisterは結果を返すだけのひな形で、保存は行いません。実際のDB・ファイル登録はRegister関数に追加してください。
非同期処理は `PageAction.Create<T>(RegisterAsync)` の形でも指定できます。
共有検証とボタンの動作確認はサンプルプロジェクトの `--verify` に含めています。

CLIではスペース区切りでモデルの各項目を渡せます。

```powershell
dotnet run --file .\samples\Tansu.Forms.SingleFile\SiteRegistration.cs -- Register --body 192.168.0.1 tokyo --envelope
dotnet run --file .\samples\Tansu.Forms.SingleFile\SiteRegistration.cs -- --help
```

このモデルでは `IpAddress` → `SiteKey` の順です。項目数が合わない場合は、必要な項目数と順序を表示して終了します。
`Subscribe<T>` がモデルの書き込み可能なJSONプロパティ順で変換し、JSONの場合と同じDataAnnotations検証を実行します。
文字列内のスペースは `"東京 本社"` のように引用符で囲んで1つの引数にしてください。
数値等はカルチャーに依存しない形式で変換します。順序を明示する場合はモデルに `JsonPropertyOrder` 属性を使用できます。
既存のJSON・`--body -`・`--body-file` も使用できます。各関数に異なる入力モデルを登録できます。
通常の `IPageAction` は従来どおりJSONのみを扱い、独自の位置指定入力は `IPositionalBodyAction`、関数一覧と関数別の位置指定入力は `IActionCatalog` の実装で追加できます。

### 既存のSQL操作名API

`PageAction.Insert<T>` / `InsertButton` / `SelectAsync` などの既存APIも引き続き使用できます。
独自関数では `ActionRequest.Function` が関数名を保持し、互換用の `Method` はSQL操作名の場合だけ値を持つ nullable enum です。

[ActionTool.cs](samples/Tansu.Forms.SingleFile/ActionTool.cs)は、引数なしでGUI、SELECTなどの引数付きでCLIとして動くサンプルです。
CLIではGUIのコールバックを実行せず、コントロールも作成しません。利用者のコードは1つの`.cs`ファイルにまとめられます。

```csharp
using Tansu.Forms.Execution;
using Tansu.Forms.Components;

return await ActionApp.RunAsync(args, new MyAction(), form =>
{
    // ここでField・ModelForm・DataGridなどを配置します。
    var get = Elements.ButtonAsync("取得", async token =>
    {
        var response = await form.Action.SelectAsync(cancellationToken: token);
        // response.Bodyを画面へ反映します。
    });
    // Layout.Rootなどにgetを追加します。
});

public sealed class MyAction : IPageAction
{
    public Task<ActionResponse> ExecuteAsync(ActionRequest request, CancellationToken token)
    {
        return Task.FromResult(request.Method switch
        {
            ActionMethod.Select => ActionResponse.Ok(new { Message = "取得結果" }),
            ActionMethod.Insert => ActionResponse.Created(request.Body),
            _ => ActionResponse.MethodNotAllowed()
        });
    }
}
```

`IPageAction.ExecuteAsync`がページの入口です。`ActionForm`へインスタンスを渡すと自動接続されます。
独自の起動処理では`FormsApp.Run(() => new ActionForm(new MyAction()));`も使えます。
呼び出し側の`SelectAsync` / `InsertAsync` / `UpdateAsync` / `DeleteAsync`は、同じ入口へ操作を指定してリクエストを送ります。
`ActionMethod`は`Select` / `Insert` / `Update` / `Delete`です。SQL由来の操作名を使いますが、SQL文の実行やDB接続を要求しません。
旧APIの`GetAsync` / `PostAsync` / `PutAsync`は、それぞれ`SelectAsync` / `InsertAsync` / `UpdateAsync`へ変更しました。
`Query`とCLIの`--query`は`Parameters`と`--param`へ変更し、`PatchAsync`は廃止しています。旧HTTP操作名はCLIで受け付けません。
`Path`は既定値`/`、`Parameters`は文字列の辞書です。パラメータ名は大文字・小文字を区別しません。
ページ内でPath・検索条件・idを解釈してください。パラメータの意味と検証はAction側で定義します。

```csharp
var response = await form.Action.SelectAsync(new Dictionary<string, string>
{
    ["partNumber"] = "P-102",
    ["minGoodCount"] = "100",
    ["maxGoodCount"] = "200"
});
```

単一ファイルのサンプルは品番の部分一致と良品数の範囲をAND条件で検索します。条件なしなら一覧を返します。
GUIにも品番検索・数量下限の入力欄があり、SELECTボタンでActionへ条件を送信します。

GUIから渡したBodyは.NETオブジェクトのまま保持し、CLIからのBodyはJSONです。
`request.ReadBody<MyInput>()`でDTOへ変換し、DataAnnotationsを検証します。追加の業務検証はActionに書きます。
DTOは送信時点の値として用意し、実行中に変更しないでください。入力用モデルと保存結果のモデルは別々にもできます。
検証失敗は400/422のResponseになり、項目名の`FieldErrors`を`editor.ApplyErrors(response.FieldErrors, response.Message)`で既存の入力欄に表示できます。
`ApplyErrors`は入力値や保存済みの基準を変更せず、次の編集時に古いエラーが消えます。

`ActionClient`は画面なしでも使用でき、ページ内で同時に実行できるリクエストは1つです。
`IsRunning` / `PendingRequest` / `LastResponse` / `LastError` / `IsCanceled`と`Cancel()`を提供します。
同時実行は`InvalidOperationException`、予期しない実行失敗とキャンセルは呼び出し元へ伝わります。
キャンセルは協調的で、既に保存されたデータを巻き戻しません。`ActionForm`を閉じると所有するClientの実行をキャンセルします。
Clientの`StateChanged`は任意のスレッドで発生します。GUIではUIスレッドへ通知される`form.ActionStateChanged`を使ってください。
Action本体はスレッドプールで実行されます。コントロールには触れず、DTOと業務処理だけを扱ってください。画面終了後もUIのメッセージループに依存せず取消が完了します。
画面部品は維持されます。取得結果の反映、保存後の再取得、未保存入力の扱いは画面側で明示します。
ギャラリーの「共通Action」から、SELECT・INSERT・UPDATE、取消と業務検証のサンプルを開けます（その画面のデータはメモリ内です）。

### CLIとPowerShellのパイプ

```powershell
# GUI
dotnet run --file samples/Tansu.Forms.SingleFile/ActionTool.cs

# SELECTの結果はJSON配列。PowerShell側でオブジェクトへ変換できます。
dotnet run --file samples/Tansu.Forms.SingleFile/ActionTool.cs --verbosity quiet -- SELECT |
    ConvertFrom-Json | Where-Object GoodCount -gt 100

# 条件をActionへ渡して検索（複数条件はAND）
dotnet run --file samples/Tansu.Forms.SingleFile/ActionTool.cs --verbosity quiet -- SELECT --param partNumber=P-102 --param minGoodCount=100

# JSONファイルからINSERTし、登録後のデータを保存
dotnet run --file samples/Tansu.Forms.SingleFile/ActionTool.cs --verbosity quiet -- INSERT --body-file input.json > created.json

# SELECTで取得した値を加工してUPDATE
$record = dotnet run --file samples/Tansu.Forms.SingleFile/ActionTool.cs --verbosity quiet -- SELECT --id 1 | ConvertFrom-Json
$record.GoodCount = 200
$record | ConvertTo-Json -Depth 20 -Compress |
    dotnet run --file samples/Tansu.Forms.SingleFile/ActionTool.cs --verbosity quiet -- UPDATE --id 1 --body -
```

引数は`SELECT|INSERT|UPDATE|DELETE [/<path>] [--id <id>] [--param name=value] [--body <json|-> | --body-file <file>] [--envelope]`です。
操作名の大文字・小文字は区別しません。`--param`は異なるキーで複数回指定できます。同じキーの重複（大小文字違いも含む）は入力エラーになります。
SELECTにはBodyを指定せず、検索条件をParametersで渡します。
`--body -`の場合だけ標準入力から1つのJSON文書を読みます。空入力・不正JSONはActionの更新処理へ渡しません。
配列Bodyを一括処理するかどうかはActionが決めます。JSON Linesや自動的な1件ずつの更新には対応していません。

成功時はBodyだけをcamelCaseのJSONで標準出力へ出します（Bodyがなければ`null`）。`--envelope`でResponse全体を出せます。
失敗時は`statusCode` / `message` / `fieldErrors`を標準エラーへ出し、標準出力に成功データを出しません。
終了コードは成功0、実行失敗1、入力/検証2、対象なし3、非対応Method4、競合5、取消130です。`--help`は使用方法を表示します。
Action内のログは`Console.Error`へ出してください。`Console.WriteLine`で説明文を混ぜるとパイプのJSONが壊れます。
`--verbosity quiet`はSDKの通常のビルド出力を抑制しますが、ビルド診断は残り得ます。自動更新処理ではSDKの失敗・終了コードも確認してください。
PowerShellのパイプは上流の非0終了だけでは下流処理を自動停止しません。取得結果を変数に受け、`$LASTEXITCODE`を確認してから更新すると失敗時の連鎖を防げます。

このサンプルは既定で`%LOCALAPPDATA%/Tansu.Forms/single-file/production.json`へ保存します。`--param store=C:/path/production.json`で別の保存先を指定できます。
SELECT / INSERT / UPDATE / DELETEを実装し、SELECTはid・partNumber・minGoodCount・maxGoodCountを組み合わせて検索できます。
partNumberは大小文字を区別しない部分一致、良品数の上下限は0〜1000000の整数です。不正な値や上下限の逆転は入力エラーになります。
id指定時は単一データを返し、他の条件も適用します（該当なしは404）。id未指定時は配列を返し、該当なしは`[]`です。
UPDATEとDELETEはidが必要です。INSERT/UPDATEのBodyは`{"partNumber":"P-102","goodCount":100}`です。UPDATEではこの入力DTOの値で更新します。
保存後の結果はidを含み、そのJSONをUPDATEの入力にも使えます。idはParametersで指定した対象が優先され、INSERTは新しいidを割り当てます。
別プロセスから同じ保存先を同時に操作すると409を返し、更新は一時ファイルからの置き換えで保存します。

GUIとCLIを併用するファイルには、既存サンプルのWinExe設定に代えて次を指定します。

```csharp
#:property OutputType=Exe
#:property DisableWinExeOutputInference=true
```

`TargetFramework=net10.0-windows` / `UseWindowsForms=true` / `PublishAot=false`はそのままです。
コンソールからGUIを起動すると、そのコンソールに接続したまま動作します。GUI専用の既存サンプルはWinExeを維持しています。

## 入力フォーム

```csharp
using Tansu.Forms.Components;
using Tansu.Forms.Layout;
using UiLayout = Tansu.Forms.Layout.Layout;

var partNumber = Field.Text("品番").Required();
var goodCount = Field.Number("良品数", min: 0);
var workDate = Field.Date("作業日");
var line = Field.Select("ライン", ("A", "ラインA"), ("B", "ラインB"));
var remarks = Field.Multiline("備考").FullWidth();

var fields = UiLayout.AutoGrid(workDate, partNumber, line, goodCount, remarks);
var actions = UiLayout.Row(Elements.Button("登録", () =>
{
    if (!partNumber.ValidateValue()) return;
    // partNumber.Value、goodCount.Valueなどを使って保存する。
}));

var screen = UiLayout.Stack(fields, actions);
screen.Dock = DockStyle.Fill;
screen.Padding = new Padding(16);
screen.AutoScroll = true;
Controls.Add(screen); // Formのコンストラクター内
```

`Form` には `Layout` イベントがあるため、上記のように型の別名 `UiLayout` を使います。
ラベル、入力欄、説明文・エラー表示は `InputField<T>` がまとめて管理します。
入力コントロールは `.Editor` から取得でき、Windows Forms標準のイベントや設定も使えます。
入力値は `.Value` で取得・設定し、変更の通知は `.ValueChanged` で受け取れます。

| 入力部品 | 値の型 | 標準コントロール |
| --- | --- | --- |
| `Field.Text` / `Field.Multiline` | `string` | `TextBox` |
| `Field.Number` | `decimal` | `NumericUpDown` |
| `Field.Date` | `DateTime`（日付部分） | `DateTimePicker` |
| `Field.Select` | `string` | `ComboBox` |
| `Field.Check` | `bool` | `CheckBox` |

`WithHelp("説明文")`、`Required()`、`ValidateWith(value => 条件, "エラー文")` を連結できます。
`Required()` は空文字・空白・nullを拒否します。数値の正数指定などは `ValidateWith` や `min` を使います。
検証は `ValidateValue()` で明示的に実行します。
数値の初期値は最小値で、必要に応じて `initialValue` を指定できます。

### IPアドレスと日付範囲

```csharp
var ip = Field.IpAddress("設備IP", version: IpAddressVersion.IPv4).Required();
var anyIp = Field.Text("接続先").IpAddress(); // 既存の文字入力にも検証を追加
var range = Field.DateRange("対象期間", DateTime.Today.AddDays(-7), DateTime.Today);

bool validIp = ip.ValidateValue();
bool validRange = range.ValidateValue();
if (validIp && validRange)
{
    string address = ip.Value;
    DateTime start = range.Value.Start;
    DateTime end = range.Value.End;
}
range.Value = new DateRange(DateTime.Today, DateTime.Today.AddDays(3));
```

IPの既定は `IpAddressVersion.Any`（IPv4/IPv6）です。`IPv4` / `IPv6` で形式を限定できます。
空欄は任意入力として許可し、必須にする場合は `.Required()` を付けます。
IPv4は4つの10進数（0〜255）だけを受け付け、短縮表記・16進数・先頭ゼロを拒否します。
[IPAddress.TryParse](https://learn.microsoft.com/en-us/dotnet/api/system.net.ipaddress.tryparse?view=net-10.0) は一部の短縮・旧式表記も許可するため、入力用の厳密なチェックを追加しています。
ホスト名・ポート・CIDR・角括弧付きURL表記は受け付けません。形式の検証であり接続確認は行いません。
独自メッセージは `.IpAddress(IpAddressVersion.IPv4, "IPv4で入力してください")` で指定できます。

日付範囲はラベル・2つの標準DateTimePicker・エラー表示をまとめた部品です。
開始日と終了日を含む日付として扱い、同日は有効、開始日が終了日より後なら検証エラーです。
初期値を省略すると今日〜今日になり、狭い領域では2つの入力を縦に並べます。
`range.Value.Contains(date)` で期間内の日付を判定できます。時刻は除いて比較します。
`ValueChanged` は標準入力の変更で通知され、`Value` の一括設定では2つの値が更新された後に1回通知されます。
カレンダーの許容日付範囲外の値を一括設定すると例外になり、元の値を保持します。
カレンダー個別の設定は `((DateRangeEditor)range.Editor).StartPicker` / `.EndPicker` から変更できます。
検索用には既存の `SearchPanel.AddDateRange` も引き続き使用できます。

### 選択肢のキーと表示名

`Field.Select` と検索パネルの `AddSelect` は `(string Key, string Value)` のペアを受け取ります。
画面にはペアの `Value` を表示し、入力部品の `.Value` は `Key` を返します。

```csharp
var line = Field.Select("ライン", ("A", "ラインA"), ("B", "ラインB"));
string key = line.Value; // "A"
line.Value = "B";        // 画面には「ラインB」を表示
```

`Dictionary<string, string>` などの `IEnumerable<KeyValuePair<string, string>>` も渡せます。
サンプルは `SampleData.LineChoices` を入力と検索で共有し、モデルにはライン名ではなく `A` / `B` / `C` を保存します。
同じ表示名の選択肢は使えますが、キーの重複は指定エラーになります。
キーは大文字・小文字を区別します。存在しないキーを入力部品の `.Value` に設定するとエラーになります。
従来の文字列だけの指定は、表示名とキーが同じ選択肢として引き続き使用できます。

### 設定済みの標準ComboBox

ラベルなしの標準 `System.Windows.Forms.ComboBox` は `Elements.ComboBox` で作れます。
文字列、キーと表示名のペア、辞書、クラスのリストを渡せます。

```csharp
var line = Elements.ComboBox(("A", "ラインA"), ("B", "ラインB"));
string? key = line.SelectedValue as string; // "A"
line.SelectedValue = "B";                 // 「ラインB」を選択

var names = Elements.ComboBox("加工", "検査", "出荷");
var dictionary = Elements.ComboBox(new Dictionary<int, string>
{
    [1] = "第一工程", [2] = "第二工程"
}, selectedIndex: 1);

var models = new List<LineOption> { new(1, "ラインA"), new(2, "ラインB") };
var modelChoice = Elements.ComboBox(models, x => x.Name, x => x.Id);
int selectedId = (int)modelChoice.SelectedValue!;
LineOption? selectedModel = modelChoice.SelectedItem as LineOption;

public sealed record LineOption(int Id, string Name);
```

`DisplayMember` / `ValueMember` / `DataSource` を設定済みです。クラスのリストでは `SelectedItem` が元のモデルを返します。
表示用・キー用のラムダには直接の公開プロパティを指定します（`x => x.Name`）。
既定は選択専用、幅220、最大12項目のドロップダウン、文字入力による候補補完、先頭選択です。
同じ表示名がある場合は、[標準ComboBoxの補完](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.combobox.autocompletemode?view=windowsdesktop-10.0)の曖昧な動作を避けるため候補補完を無効にします。
空のデータは未選択になり、`selectedIndex: -1` で最初から未選択にもできます。
キーはnull・重複不可、表示名の重複は使用できます。渡した一覧は作成時にコピーするため、後から元の一覧に追加した項目は自動反映しません。
通常のComboBoxなので、イベントや幅、選択値などは標準プロパティで変更できます。
ラベル・説明文・検証も必要な場合は従来の `Field.Select` を使ってください。

## ボタンとコマンド

従来の `Elements.Button("登録", Save)` はそのまま使用できます。
非同期処理には `ButtonAsync` を使い、キャンセルトークンを処理へ渡します。

```csharp
var save = Elements.ButtonAsync("保存", async token =>
{
    await SaveAsync(token); // 保存処理はアプリ側で実装
});
save.BusyText = "保存中…";
save.ExecutionFailed += (_, e) => status.Text = e.Exception.Message;
var cancel = Elements.Button("取り消す", save.Cancel);
save.AsyncCommand.StateChanged += (_, _) => cancel.Enabled = save.AsyncCommand.CanBeCanceled;
cancel.Enabled = false;
```

処理中はボタンを無効化し、表示を `BusyText` に変更します。重複した実行要求は開始しません。
終了時には元の表示と実行可否へ戻り、ボタンのDispose時には所有するコマンドをキャンセルします。
キャンセルは協調式なので、アプリの処理もトークンを監視してください。
失敗は `ExecutionFailed` と `AsyncCommand.LastError` で通知します。自動でMessageBoxは開きません。
ボタンの操作による失敗は通知へ渡し、明示的に `await save.ExecuteAsync()` した場合は例外を呼び出し元へ返します。
`ExecuteAsync` の結果は成功時true、実行中・実行不可などで開始しなかった場合falseです。

`Elements.Button("保存", command, parameter)` はWinForms標準の `ICommand` / `CommandParameter` に対応します。
`AsyncCommand` も `ICommand` として使用でき、`IsRunning`・`IsCanceled`・`LastError` と `INotifyPropertyChanged` を提供します。
外部の状態で実行可否が変わった場合は `NotifyCanExecuteChanged()` を呼びます。

```csharp
var command = new AsyncCommand(token => SaveAsync(token), () => editor.IsDirty && !editor.HasErrors);
editor.StateChanged += (_, _) => command.NotifyCanExecuteChanged();
var button = Elements.Button("保存", command);
```

手動で作成したコマンドは画面の使用中保持し、画面のDispose等でDisposeしてください。
標準Buttonは共有コマンドを所有・Disposeしません。UI用コマンドはUIスレッドで作成・実行します。
ギャラリーの「入力・表」では、変更・検証状態と連動した非同期登録、取消、リセットを試せます。

## モデルの項目名とUIテスト

選択肢のキー/表示名、モデルのプロパティ名/DisplayNameは従来どおり別です。
`Field.Text` はラベルとTextBoxをまとめた部品です。入力を行うTextBoxは `.Editor` から取得できます。
ラベルや外枠は表示・配置を担当し、独自の識別子APIは持ちません。
入力コントロールの識別には標準の `Editor.Name` を使用します。

```csharp
var part = Field.Text("品番");
part.Editor.Name = nameof(ProductionRecord.PartNumber); // 入力を受けるTextBoxの識別
```

`ModelForm.Fields` と `ErrorsByField` はモデルのプロパティ名で項目を識別します。
`ModelForm` は同じプロパティ名を入力コントロールのNameへ自動設定します。
例えば `PartNumber` の入力エディターのNameは `PartNumber` で、表示や読み上げ用のラベルは「品番」のままです。
検索の入力コントロールは `PartNumber.search`、日付範囲は `WorkDate.from` / `WorkDate.to` と命名します。
UIテスト用のボタン等にも、必要な箇所だけ標準のNameを設定します。

## レイアウト

| 部品 | 動作 |
| --- | --- |
| `UiLayout.Stack(...)` | 縦積み。子要素をコンテナーの幅に合わせ、高さは内容から計算 |
| `UiLayout.Row(...)` | 左から横並び。折り返しなし |
| `UiLayout.Wrap(...)` | 左から横並び。入りきらない場合は次の行へ |
| `UiLayout.AutoGrid(...)` | 入力欄を等幅で配置。利用可能な幅から列数と行数を計算 |
| `.FullWidth()` | AutoGridで1行全体を使用 |
| `.FillRemainingHeight()` | Stackの残りの高さを使用。複数ある場合は均等に分配 |

`AutoGrid.MinimumFieldWidth` の既定値は220、`Stack.Gap` と `AutoGrid.Gap` は12です。
これらは96 DPIを基準とする値で、実行環境のDPIに合わせて調整します。
座標・行番号・列番号の指定は不要です。
`fields.Add(Field.Text("作業者"))` や `fields.Controls.Remove(control)` で後から変更できます。
`FullWidth()` はその前後で改行します。最後の行に空きがある場合、入力欄は通常の列幅を維持します。

Row/Wrapは標準の `FlowLayoutPanel`、Stack/AutoGridは通常の `Panel` 上で配置を計算します。
すべての部品は `Control` として利用でき、標準コントロールとも組み合わせられます。
画面全体はStackを `DockStyle.Fill` にし、高さが足りない画面では `AutoScroll = true` を指定します。

## ルートサイズと最小サイズ

フォーム全体のルートを `UiLayout.Root` で作ると、サイズをプリセットから選べます。
ルートはフォームへ自動的に追加され、`DockStyle.Fill`、縦スクロール、余白も設定します。
呼び出し後に `Controls.Add(root)` を実行する必要はありません。

```csharp
var root = UiLayout.Root(this, RootSize.HdPlus,
    fields,
    actions,
    search,
    grid.FillRemainingHeight());

// 実行中に別のサイズへ切り替えられます。
root.UseSize(RootSize.FullHd);
```

| プリセット | 内容領域のサイズ | 比率 |
| --- | --- | --- |
| `RootSize.Compact` | 960 × 540 | 16:9 |
| `RootSize.Hd` | 1280 × 720 | 16:9 |
| `RootSize.HdPlus` | 1600 × 900 | 16:9 |
| `RootSize.FullHd` | 1920 × 1080 | 16:9 |
| `RootSize.Small` | 640 × 480 | 4:3 |
| `RootSize.Standard` | 800 × 600 | 4:3 |
| `RootSize.StandardLarge` | 1024 × 768 | 4:3 |
| `RootSize.Wide` | 1280 × 800 | 16:10 |
| `RootSize.WideLarge` | 1440 × 900 | 16:10 |
| `RootSize.Workspace` | 1200 × 800 | 3:2 |
| `RootSize.Square` | 800 × 800 | 1:1 |
| `RootSize.Portrait` | 600 × 800 | 3:4 |

QHDと4Kは削除しました。上限はFull HDです。
WinFormsのダイアログ・標準フォーム・作業画面で使いやすい代表的な寸法を選んでいます。統一された公式の標準サイズではありません。
[Windowsのウィンドウ設計指針](https://github.com/MicrosoftDocs/win32/blob/docs/desktop-src/uxguide/top-violations.md)の、内容が使えなくなるサイズより小さくしないこと・小さい画面にも収めることを基準にしています。

指定するのはタイトルバーと枠を除いた `Form.ClientSize` のピクセル数です。
プリセットの適用時はDPI倍率をサイズに重ねて掛けず、入力コントロールなどのDPI対応は従来どおりです。
画面に収まらないプリセットは、タイトルバー、枠、タスクバーの領域を考慮して選択した比率のまま縮小します。
したがって、Full HDのモニター上でFull HDのプリセットを選ぶ場合も、実際の内容領域は少し小さくなることがあります。

指定したピクセル数をそのまま使う場合は、画面に収める処理を無効にできます。

```csharp
root.FitToWorkingArea = false;
root.UseSize(RootSize.FullHd);
```

プリセットは初期サイズと明示的な切り替え時のサイズです。
通常のウィンドウ操作では最小サイズまで自由にリサイズでき、比率への固定はしません。
自動列数調整とスクロールは、変更後のサイズに追従します。
`RootSizes.Presets` で選択肢一覧、`RootSizes.GetSize(...)` で指定サイズを取得できます。
`RootSizes.GetPreset(...)` から比率、`RootSizes.GetMinimumClientSize(...)` から推奨最小内容サイズを取得できます。
サンプルでは画面上部の選択欄から12種類を切り替えられ、初期値はHD+です。

最小サイズはルートが自動設定します。通常は640×480、小さいダイアログは480×360、正方形は480×480、縦長は480×640です。
これらは96 DPIでの内容領域の論理ピクセルです。DPIに応じて拡大し、選択したプリセットの表示可能なサイズ以内に収め、タイトルバーと枠を加えて `Form.MinimumSize` に設定します。
プリセット切替やDPI変更にも追従します。アプリが先に `Form.MinimumSize` を設定している場合は、その値を優先します。
[Form.MinimumSize](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.form.minimumsize?view=windowsdesktop-10.0) はタイトルバーや枠を含むウィンドウ全体のサイズです。

```csharp
root.MinimumClientSize = new Size(560, 420); // 最小内容サイズを論理ピクセルで変更
root.MinimumClientSize = Size.Empty;        // プリセットの推奨値に戻す
root.AutoMinimumSize = false;              // 自動設定を解除し、ルート作成前の最小サイズへ戻す
MinimumSize = new Size(480, 400);           // ウィンドウ全体をアプリ側で指定する場合
```

## データ表示と検索

モデルクラスの公開プロパティから列を自動生成し、`DisplayName` 属性をヘッダーに使います。
属性がない場合はプロパティ名を使い、`[Browsable(false)]` のプロパティは表示しません。
列名を画面側で列ごとに指定する必要はありません。

```csharp
using System.ComponentModel;

public sealed class ProductionRecord
{
    [DisplayName("作業日")]
    public DateTime WorkDate { get; set; }

    [DisplayName("品番")]
    public string PartNumber { get; set; } = "";

    [DisplayName("ライン")]
    public string Line { get; set; } = "";

    [DisplayName("良品数")]
    public int GoodCount { get; set; }
}
```

フォームでは `List<T>` を渡します。空のリストから始める場合も列は生成されます。

```csharp
var records = new List<ProductionRecord>
{
    new() { WorkDate = DateTime.Today, PartNumber = "P-102", Line = "A", GoodCount = 120 }
};

var grid = new DataGrid<ProductionRecord>().SetData(records)
    .FormatColumn(nameof(ProductionRecord.GoodCount), "N0")
    .FormatColumn(nameof(ProductionRecord.WorkDate), "yyyy/MM/dd");

var search = new SearchPanel()
    .AddText(nameof(ProductionRecord.PartNumber), "品番")
    .AddSelect(nameof(ProductionRecord.Line), "ライン", ("A", "ラインA"), ("B", "ラインB"))
    .AddDateRange(nameof(ProductionRecord.WorkDate), "作業日", DateTime.Today.AddDays(-7), DateTime.Today)
    .Connect(grid);

var screen = UiLayout.Stack(search, grid.FillRemainingHeight());
screen.Dock = DockStyle.Fill;
Controls.Add(screen);
```

`DataGrid<T>` の `T` はデータクラスです。`SetData` は `List<T>` を受け取ります。
検索対象は表示名ではなくプロパティ名で指定するため、`nameof` を使います。
テキスト検索と選択肢検索は `string`、日付範囲検索は `DateTime` / `DateTime?` のプロパティに対応します。
テーブルは読み取り専用で、列ヘッダーをクリックすると昇順・降順にソートできます。
数値・日付は元のデータ型で並び替え、表示形式は `FormatColumn` で指定します。
`bool` のプロパティは標準のチェックボックス列になります。
内部でソート可能な `BindingList<T>` を使い、元のモデルオブジェクトをそのままバインドします。
元のリストの並び順は変更せず、同じリストを渡した複数のグリッドも検索・ソート状態を個別に持ちます。

`List<T>` は変更通知を持たないため、追加・削除やプロパティ変更の後に `RefreshData()` を呼び出します。
適用済みの検索条件とソート順は維持されます。

```csharp
records.Add(new ProductionRecord
{
    WorkDate = DateTime.Today, PartNumber = "P-205", Line = "B", GoodCount = 85
});
grid.RefreshData();

ProductionRecord? selected = grid.SelectedItem;
IReadOnlyList<ProductionRecord> displayed = grid.Items;

// 検索パネルを使わず、型付きの条件で絞り込むことも可能。
grid.ApplyFilter(record => record.GoodCount >= 100);
grid.ClearFilter();
```

検索ボタン、または文字検索欄のEnterで、すべての条件をANDで適用します。
テキスト検索は大文字・小文字を区別しない部分一致です。引用符、`%`、`*`、角括弧なども文字そのものとして扱います。
日付範囲の終了日はその日の終わりまで含みます。
「条件をクリア」は入力を初期状態に戻し、日付条件も含めてフィルターを解除して全件表示します。
初期状態ではフィルターを適用しません。必要なら `search.ApplySearch()` を呼び出してください。
`AddSelect` は先頭に「すべて」を自動追加し、選択したキーをモデルのプロパティと完全一致で比較します。
表示名は検索条件には使いません。

```csharp
var search = new SearchPanel()
    .AddSelect(nameof(ProductionRecord.Line), "ライン", new Dictionary<string, string>
    {
        ["A"] = "ラインA",
        ["B"] = "ラインB"
    })
    .Connect(grid);

string? key = search.GetSelectedKey(nameof(ProductionRecord.Line));
// 「ラインA」なら "A"、「すべて」なら null。
```

`AddSelect` 自体の戻り値は引き続き `SearchPanel` で、メソッドを連結できます。
選択したキーの取得には `GetSelectedKey` を使います。
「すべて」は通常の選択肢と区別されるため、空文字や `すべて` というキーも実データのキーとして使用できます。
検索欄の値を変更しただけでは、適用済みの検索条件は変わりません。
`grid.SelectedItem` から選択された元のモデルオブジェクトを取得でき、`grid.Items` は表示中のモデルの一覧です。

メモリ内のリスト向けです。ページングはライブラリが提供し、DB検索と永続化はアプリ側で実装します。

## ページングとモデルフォーム

```csharp
var pages = new Pagination(pageSize: 10);
using var paging = grid.ConnectPagination(pages);
grid.SetColumnVisible(nameof(ProductionRecord.Remarks), false);
grid.Grid.MultiSelect = true;
IReadOnlyList<ProductionRecord> selected = grid.SelectedItems;
```

フィルター後にページを先頭へ戻し、表示対象件数からページ数を更新します。
`RowCount` は現在のページの行数、`FilteredCount` は検索後の総件数です。
ページングしない場合は従来どおり全件を表示します。

```csharp
var editor = new ModelForm<ProductionRecord>(record);
bool changed = editor.IsDirty;
if (editor.TryRead(out var model))
{
    // 入力値を新しいモデルとして取得。元のrecordは変更しない。
}
editor.Reset();
```

`DisplayName` からラベルを作り、`Browsable(false)` と読み取り専用プロパティを除外します。
`TryRead` は読み込んだモデルを浅くコピーして入力値を反映するため、非表示項目や読み取り専用の状態も保持します。
複雑な参照型の値はコピー元と共有されます。参照先を編集する場合の深いコピーはアプリ側で実装してください。
標準の文字入力、日付、bool、enumを使い、数値等は型変換とDataAnnotationsで検証します。
`Errors` はエラー一覧、`Fields` はプロパティ名から入力部品を取得する辞書です。
参照型を含む複雑なプロパティは `Browsable(false)` で除外し、`Field.Custom` で専用の入力を合成してください。

`ValueChanged` は変更したプロパティ名を通知し、`LoadModel` / `Reset` ではPropertyName=nullの通知を1回発行します。
`IsDirtyChanged` は未保存状態の切り替わり、`ValidationChanged` は検証状態やエラーの変更、`StateChanged` はフォーム状態の更新を通知します。
`INotifyPropertyChanged` にも対応しています。

```csharp
var editor = new ModelForm<ProductionRecord>(record) { ValidateOnChange = true };
editor.ValidationChanged += (_, _) => summary.Text = string.Join(" / ", editor.Errors);
editor.IsDirtyChanged += (_, _) => status.Text = editor.IsDirty ? "未保存の変更があります" : "変更なし";

if (!editor.TryRead(out var model))
    editor.FocusFirstError();
// 保存が成功した後、入力値を保存済み/リセット基準として確定する。
// editor.AcceptChanges();
```

`ValidateOnChange` は既定でfalseで、trueにすると入力変更時に型変換とDataAnnotationsを検証します。
`ErrorsByField` はプロパティ名からエラー一覧を取得する辞書、`HasErrors` は検証エラーの有無です。
`IsValidated` がfalseの場合は未検証です。エラーがないだけで有効とは判断せず、実行前には `TryRead` を使用してください。
`Reset` / `LoadModel` / `ClearValidation` は検証状態を未検証に戻します。
`AcceptChanges()` は現在の入力を検証し、成功した場合だけ新しい保存済み基準へ切り替えます。
非同期保存中は入力を無効化するなど、保存した値と確定する値を一致させてください。
標準エディターを `Field.Custom` で包む場合もValueChangedが自動通知されます。
独自エディターでは、その変更イベントから `field.NotifyValueChanged()` を呼びます。

## デザインテンプレート

既定値はWindows Forms標準です。デザインを変更するときはフォーム全体へテーマセッションを付けます。

```csharp
using var theme = UiTheme.Attach(this);
theme.Use(ThemeTemplate.Load("themes/normal.json")); // 緑を基調とした明るいテーマ
theme.Use(ThemeTemplate.Load("themes/dark.json"));
theme.Use(ThemeTemplate.Native); // ネイティブのフォント、色、余白へ復帰
```

セッションはフォームを使っている間保持してください。コンストラクター内で `using` を完了すると即座に元へ戻ります。
通常はフォームのフィールドに保存し、フォームのDisposeでセッションをDisposeします。
フォームがDisposeされたときもセッションは解放されます。
後から追加したコントロールにも現在のテーマが適用されます。
部品の外観はソースを書き換えず、JSONの色・フォント・余白・ロール・チャート色で差し替えられます。
`WithRole("danger")` のように意味を付け、ロール別の色やフォントも設定できます。

```json
{
  "name": "MyTemplate",
  "fontSize": 10,
  "background": "#F4F4F4",
  "foreground": "#202020",
  "gap": 12,
  "rootPadding": 16,
  "roles": {
    "input": { "background": "#FFFFFF" },
    "heading": { "fontSize": 18 },
    "danger": { "foreground": "#B00020" }
  },
  "chartColors": ["#2365A0", "#33855B", "#C58B30"]
}
```

省略した値は元の標準設定を維持します。色は `#RRGGBB` 形式です。
`themes/native.json`、`themes/normal.json`、`themes/dark.json` はサンプル出力先にもコピーされ、ギャラリーで切替できます。
Normalはマテリアルカラーを参考にした緑・白の配色です。Nativeは従来どおり標準表示です。
`UiTheme.Inherit(child, owner)` は独立したポップアップへテーマを引き継ぎ、所有者側の切替にも追従します。
OSが描画するカレンダー等の詳細な外観は、標準コントロールで設定可能なプロパティの範囲です。

### 角の丸み

JSONに `"cornerRadius": 8` を追加すると、ボタン・選択・数値入力を角丸にします。TextBox・RichTextBoxは文字や標準枠の欠けを防ぐため、角丸の対象外です。
値は96 DPIでの論理ピクセルで、DPI変更やサイズ変更にも追従します。
ロール別には `"roles": { "button": { "cornerRadius": 12 } }` と指定できます。
Normalの既定値は8、NativeとDarkは角丸を指定しません。

```csharp
var save = Elements.Button("登録", Save).WithRadius(12);
var square = Elements.Button("取消", Cancel).WithRadius(0);
var input = Field.Text("品番");
// TextBoxのEditorには角丸を適用しません。
save.ResetRadius(); // 個別指定を解除し、現在のテーマに従う
```

個別指定はテーマより優先されます。`WithRadius` はTextBoxBase以外のControlに適用できます。TextBoxBaseでは指定を無視し、標準の表示を維持します。
標準コントロールのRegionで外周を丸くするため、OS標準の枠線は角の部分で切り取られます。
フォームやレイアウト全体は自動で丸めません。テーマをNativeに戻すとテーマ由来の丸みは元のRegionへ戻ります。

## ガント

```csharp
var tasks = new List<GanttTask>
{
    new() { Id = "cutting", Title = "切断", Resource = "ラインA",
        Start = DateTime.Today, End = DateTime.Today.AddDays(3), Progress = 40 },
    new() { Id = "inspection", Title = "検査", Resource = "検査室",
        Start = DateTime.Today.AddDays(4), End = DateTime.Today.AddDays(5) }
};
var gantt = new GanttChart().SetData(tasks);
gantt.TimelineScale = GanttScale.Week; // Day / Week / Month
gantt.SelectionChanged += (_, _) => Console.WriteLine(gantt.SelectedTask?.Id);
Controls.Add(gantt);
gantt.Dock = DockStyle.Fill;
tasks[0].Progress = 60;
gantt.RefreshData();
```

開始日・終了日は時刻を除いた日付で、終了日を含みます。同日の作業も1日分のバーです。
IDは必須かつ一意、進捗は0〜100、全作業の期間は100年以内です。
クリック・上下/Home/Endキーで元のモデルを選択し、選択行へ自動スクロールします。
担当表示、進捗バー、週末の背景、今日の線、ツールチップ、縦横のスクロールがあります。
`FitToTasks()` は表示期間を全作業に合わせ、`SetViewRange(start, end)` は期間を指定し、`ScrollToDate(date)` は日付へ移動します。
`Items` は渡したListのモデルを参照します。Listの追加・削除・値の変更後は `RefreshData()` を呼んでください。
表示は読み取り専用です。日付の編集、工程の依存関係や自動スケジューリングはアプリ側で扱います。
ギャラリーの「ガント」タブに製造工程のサンプル、表示単位切替、進捗更新の例があります。

## 追加した対話機能

- `AccordionPanel` / `CollapsibleSection`: 開閉、単一/複数展開、無効化。
- `AspectRatioPanel` / `CarouselPanel` / `Breadcrumb`: 比率、前後移動・ループ、階層リンクと省略メニュー。
- `InputGroup` / `ChoicePicker<TKey>` / `MultiSlider`: 入力の前後・上下の内容、グループ検索と複数選択、交差しない複数の値。
- `CommandPalette`: グループ・名前・ショートカットで検索し、Enterで実行、Escで閉じる。
- `SurfacePopup` / `SheetForm` / `DrawerForm`: ホバーの遅延、操作可能な内容、4方向、所有者追従、ドラッグとスナップ。
- `AttachmentView` / `AttachmentList`: ファイルの名前・サイズ・プレビュー・進捗・エラー・表示・削除。
- `ToastHost`: 期限付きの通知、操作ボタン、`TrackAsync` による処理中・成功・エラーの表示。
- `Chart`: 7種類の描画、複数系列・積み上げ、凡例、ヒントとキーボードでの値の読取。
- `Questionnaire`: 選択・複数選択・自由入力、条件付き質問、必須・独自検証・スキップ、保存・再開・送信。
- `MessageBubble` / `ConversationMessage` / `MessageScroller`: 会話本文、添付・操作・リアクション、履歴とストリーム、読書位置、ジャンプ、仮想化。
- `NaturalDates` / `Field.DateText`: 明日、来週月曜、next Friday等の文字入力から日付を取得。

メッセージの自動追従は既定で無効です。`FollowLatest` で明示的に追従し、`PauseFollowing` で停止します。
更新は `UpdateMessage`、以前の履歴追加は `PrependHistory`、再開位置は `CapturePosition` / `RestorePosition` を使います。
`Virtualize = true` は画面外の行を軽いパネルへ置き換え、IDでのジャンプ時に内容を再接続します。
メッセージ内容・履歴・保存・AI通信はアプリ側の責務です。通知や添付にも隠れた外部通信はありません。
添付のアップロード処理はアプリが実装し、`SetState` で表示状態を更新します。

## 配布とソースの利用

NuGetパッケージの作成:

```powershell
dotnet pack Tansu.Forms.slnx -c Release -o artifacts/packages
```

`Tansu.Forms.Layout` / `Tansu.Forms.Execution` / `Tansu.Forms.Components`の3つのパッケージを作ります。
ComponentsパッケージはLayoutとExecutionを依存として解決するので、同じフィードに3つとも置いてください。
Layoutだけを使う場合は `Tansu.Forms.Layout` パッケージだけを参照します。

shadcn/uiのように実装ソースを所有して変更したい場合は、3つのライブラリプロジェクトをまとめてコピーできます。
出力にはLayout・Execution・Componentsのソース、プロジェクト参照、テーマJSON、READMEを含めます。
`components.json` が各プロジェクトの依存関係とソースファイルの対応を保持します。

```powershell
./scripts/Export-Source.ps1 -Destination C:/source/MyApp/UiSource
dotnet build C:/source/MyApp/UiSource/Tansu.Forms.Components/Tansu.Forms.Components.csproj
```

既存ファイルへ上書きする場合だけ `-Force` を指定してください。エクスポートはPowerShell 7で実行します。
これは.NETでのソース配布方法で、npm/ReactのCLIやWeb用レジストリーサーバーは再実装しません。

## 動作確認

WinFormsに依存しないAction・CLIの検証:

```powershell
dotnet run --project tests/Tansu.Forms.Execution.Tests
dotnet run --file samples/Tansu.Forms.SingleFile/ActionTool.cs -- --verify
./scripts/Verify-Execution.ps1
```

任意関数の登録・解除、関数一覧付きヘルプ、関数別の入力モデル、操作/Path/Parameters、型付きBody、入力検証、標準入力・ファイル入力、JSONの連結、標準エラー・終了コード、同時実行防止・取消を確認します。
結果は`artifacts/execution-verification/results.txt`に保存します。
`Verify-Execution.ps1`は一時保存先で実際の単一ファイルアプリと発行したexeを動かし、PowerShellでのパイプ、別プロセス間の保存・取得、エラー・終了コードを検証します。
結果は`artifacts/cli-verification/results.txt`に保存します。

サンプルプロジェクト内にコントロール・レイアウト・コマンド・フォーム通知の検証モードがあります。

```powershell
dotnet run --project samples/Tansu.Forms.Sample -- --verify
```

実際のコントロールを使って、自動配置、DPIを考慮したリサイズ、縦スクロール、
ルートサイズの選択、各比率の内容領域とモニターへの縮小、自動最小サイズと上書き、
項目の追加・削除・表示切り替え、必須チェック、登録、検索、ソート、
DisplayNameによる列生成、特殊文字の検索、日付境界、グリッド間のフィルターの独立性、
選択肢の表示名とキーの分離、キーでの検索と取得、辞書形式の入力、
リスト更新後の検索・ソートの維持、空のリスト、nullを含むプロパティのソートを検証します。
検証フォームは画面外に表示し、終了時に閉じます。
実行したディレクトリの `artifacts/verification/` に結果と画面画像を保存します。
終了コードは成功時0、失敗時1です。

外部UIテストは別プロジェクトで実行します。テスト用依存はFlaUI.UIA3のみで、ライブラリ本体には追加しません。

```powershell
dotnet run --project tests/Tansu.Forms.UiTests
```

サンプルの専用画面を別プロセスで起動し、UI AutomationのValue/Invokeパターンで入力・ボタンを操作します。
プロパティ名による識別、表示名、必須/型変換のエラー、未保存状態、非同期登録・取消・失敗、保存後のリセットを確認します。
Windowsの対話セッションが必要です。検証画面はタスクバーに表示せず画面外へ配置し、テスト終了時に起動したプロセスだけを閉じます。
結果は `artifacts/ui-verification/results.txt` に保存し、失敗時は終了コード1を返します。
