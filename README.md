# WinformsUI

Windows Formsの標準コントロールを、コードだけで組み合わせるUIライブラリです。
入力欄の上にラベルを表示し、項目数とウィンドウ幅に合わせて配置します。
デザイナーや `.Designer.cs`、外部UIパッケージは使用しません。

## プロジェクト

| プロジェクト | 内容 |
| --- | --- |
| `src/WinformsUI` | 入力部品、レイアウト、データグリッド、検索パネル |
| `samples/WinformsUI.Sample` | 製造実績の登録・検索・ソートと自動配置を試せるアプリ |

必要な環境は **Windowsと.NET 10 SDK** です。ソリューションは `WinformsUI.slnx` です。

shadcn/uiの公式一覧64項目に対応する機能を、[コンポーネント対応表](docs/COMPONENTS.md)に整理しています。
25項目は標準部品にない動作を合成部品で補い、39項目はWindows Forms標準のAPIを使用します。
WebのDOM/SSRやReact/Tailwindの実装方式は.NETへ移植せず、UIの操作・データ・状態の機能を対象としています。

```powershell
dotnet build WinformsUI.slnx
dotnet run --project samples/WinformsUI.Sample
```

全体の部品ギャラリーは、実績サンプルの「コンポーネント一覧」ボタン、または次のコマンドで開けます。

```powershell
dotnet run --project samples/WinformsUI.Sample -- --gallery
```

サンプルでは「項目を追加」「追加項目を削除」で動的な配置を確認できます。
ウィンドウを狭くすると入力欄が少ない列に並び替わり、高さが足りない場合は縦にスクロールします。
追加項目は配置の確認用で、実績の保存対象には含みません。
実績はメモリ内の `List<ProductionRecord>` に保存され、終了すると消えます。
データクラスは `ProductionRecord.cs`、初期データの作成は `SampleData.cs` にまとめています。

## 入力フォーム

```csharp
using WinformsUI;
using UiLayout = WinformsUI.Layout;

var partNumber = Field.Text("品番").Required();
var goodCount = Field.Number("良品数", min: 0);
var workDate = Field.Date("作業日");
var line = Field.Select("ライン", ("A", "ラインA"), ("B", "ラインB"));
var remarks = Field.Multiline("備考").FullWidth();

var fields = UiLayout.AutoGrid(workDate, partNumber, line, goodCount, remarks);
var actions = UiLayout.Row(Ui.Button("登録", () =>
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

## ルートサイズ（16:9）

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

| プリセット | 内容領域のサイズ |
| --- | --- |
| `RootSize.Compact` | 960 × 540 |
| `RootSize.Hd` | 1280 × 720 |
| `RootSize.HdPlus` | 1600 × 900 |
| `RootSize.FullHd` | 1920 × 1080 |
| `RootSize.Qhd` | 2560 × 1440 |
| `RootSize.Uhd4K` | 3840 × 2160 |

指定するのはタイトルバーと枠を除いた `Form.ClientSize` のピクセル数です。
プリセットの適用時はDPI倍率をサイズに重ねて掛けず、入力コントロールなどのDPI対応は従来どおりです。
画面に収まらないプリセットは、タイトルバー、枠、タスクバーの領域を考慮して16:9のまま縮小します。
したがって、Full HDのモニター上でFull HDのプリセットを選ぶ場合も、実際の内容領域は少し小さくなることがあります。

指定したピクセル数をそのまま使う場合は、画面に収める処理を無効にできます。

```csharp
root.FitToWorkingArea = false;
root.UseSize(RootSize.FullHd);
```

プリセットは初期サイズと明示的な切り替え時のサイズです。
通常のウィンドウ操作では自由にリサイズでき、16:9への固定はしません。
自動列数調整とスクロールは、変更後のサイズに追従します。
`RootSizes.Presets` で選択肢一覧、`RootSizes.GetSize(...)` で指定サイズを取得できます。
サンプルでは画面上部の選択欄から6種類を切り替えられ、初期値はHD+です。

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
標準の文字入力、日付、bool、enumを使い、数値等は型変換とDataAnnotationsで検証します。
`Errors` はエラー一覧、`Fields` はプロパティ名から入力部品を取得する辞書です。
参照型を含む複雑なプロパティは `Browsable(false)` で除外し、`Field.Custom` で専用の入力を合成してください。

## デザインテンプレート

既定値はWindows Forms標準です。デザインを変更するときはフォーム全体へテーマセッションを付けます。

```csharp
using var theme = UiTheme.Attach(this);
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
`themes/native.json` と `themes/dark.json` はサンプル出力先にもコピーされ、ギャラリーで切替できます。
`UiTheme.Inherit(child, owner)` は独立したポップアップへテーマを引き継ぎ、所有者側の切替にも追従します。
OSが描画するカレンダー等の詳細な外観は、標準コントロールで設定可能なプロパティの範囲です。

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
dotnet pack src/WinformsUI -c Release -o artifacts/packages
```

shadcn/uiのように実装ソースを所有して変更したい場合は、ソースとスタンドアロンのプロジェクトをコピーできます。
相互依存の取りこぼしを避けるため、コピーは小さいライブラリ全体を単位とします。
`components.json` がコンポーネントとソースファイルの対応を保持します。

```powershell
./scripts/Export-Source.ps1 -Destination C:/source/MyApp/UiSource
```

既存ファイルへ上書きする場合だけ `-Force` を指定してください。エクスポートはPowerShell 7で実行します。
これは.NETでのソース配布方法で、npm/ReactのCLIやWeb用レジストリーサーバーは再実装しません。

## 動作確認

サンプルプロジェクト内に検証モードがあり、テスト用の第3プロジェクトはありません。

```powershell
dotnet run --project samples/WinformsUI.Sample -- --verify
```

実際のコントロールを使って、自動配置、DPIを考慮したリサイズ、縦スクロール、
ルートサイズの選択、16:9の内容領域とモニターへの縮小、
項目の追加・削除・表示切り替え、必須チェック、登録、検索、ソート、
DisplayNameによる列生成、特殊文字の検索、日付境界、グリッド間のフィルターの独立性、
選択肢の表示名とキーの分離、キーでの検索と取得、辞書形式の入力、
リスト更新後の検索・ソートの維持、空のリスト、nullを含むプロパティのソートを検証します。
検証フォームは画面外に表示し、終了時に閉じます。
実行したディレクトリの `artifacts/verification/` に結果と画面画像を保存します。
終了コードは成功時0、失敗時1です。
