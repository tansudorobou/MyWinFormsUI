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

```powershell
dotnet build WinformsUI.slnx
dotnet run --project samples/WinformsUI.Sample
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
var line = Field.Select("ライン", "ラインA", "ラインB");
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
    new() { WorkDate = DateTime.Today, PartNumber = "P-102", Line = "ラインA", GoodCount = 120 }
};

var grid = new DataGrid<ProductionRecord>().SetData(records)
    .FormatColumn(nameof(ProductionRecord.GoodCount), "N0")
    .FormatColumn(nameof(ProductionRecord.WorkDate), "yyyy/MM/dd");

var search = new SearchPanel()
    .AddText(nameof(ProductionRecord.PartNumber), "品番")
    .AddSelect(nameof(ProductionRecord.Line), "ライン", "ラインA", "ラインB")
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
    WorkDate = DateTime.Today, PartNumber = "P-205", Line = "ラインB", GoodCount = 85
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
検索欄の値を変更しただけでは、適用済みの検索条件は変わりません。
`grid.SelectedItem` から選択された元のモデルオブジェクトを取得でき、`grid.Items` は表示中のモデルの一覧です。

初版はメモリ内のリスト向けです。DB検索、ページング、永続化はアプリ側で実装します。

## 動作確認

サンプルプロジェクト内に検証モードがあり、テスト用の第3プロジェクトはありません。

```powershell
dotnet run --project samples/WinformsUI.Sample -- --verify
```

実際のコントロールを使って、自動配置、DPIを考慮したリサイズ、縦スクロール、
ルートサイズの選択、16:9の内容領域とモニターへの縮小、
項目の追加・削除・表示切り替え、必須チェック、登録、検索、ソート、
DisplayNameによる列生成、特殊文字の検索、日付境界、グリッド間のフィルターの独立性、
リスト更新後の検索・ソートの維持、空のリスト、nullを含むプロパティのソートを検証します。
検証フォームは画面外に表示し、終了時に閉じます。
実行したディレクトリの `artifacts/verification/` に結果と画面画像を保存します。
終了コードは成功時0、失敗時1です。
