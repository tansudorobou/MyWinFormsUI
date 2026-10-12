using UiLayout = Tansu.Forms.Layout.Layout;

namespace Tansu.Forms.Sample;

public sealed class SampleForm : Form
{
    internal readonly List<ProductionRecord> Records = SampleData.CreateRecords();
    private readonly Label _status = Elements.Label("入力して登録してください。データは終了時に消えます。");
    private readonly Label _count = Elements.Label("");
    private readonly List<InputField<string>> _extraFields = [];
    internal readonly InputField<DateTime> WorkDate = Field.Date("作業日");
    internal readonly InputField<string> PartNumber = Field.Text("品番").Required();
    internal readonly InputField<string> Line = Field.Select("ライン", SampleData.LineChoices);
    internal readonly InputField<decimal> GoodCount = Field.Number("良品数", min: 0);
    internal readonly InputField<decimal> BadCount = Field.Number("不良数", min: 0);
    internal readonly InputField<string> Remarks = Field.Multiline("備考").FullWidth();
    internal readonly AutoGridPanel FormFields;
    internal readonly DataGrid<ProductionRecord> RecordsGrid;
    internal readonly SearchPanel Search;
    internal readonly Button SaveButton;
    internal readonly Button AddFieldButton;
    internal readonly Button RemoveFieldButton;
    internal readonly RootPanel Screen;
    internal readonly ComboBox SizeSelector;

    public SampleForm()
    {
        Text = "Tansu.Forms サンプル — 標準コントロール / コードだけで作成";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;

        WorkDate.Editor.Name = nameof(ProductionRecord.WorkDate);
        PartNumber.Editor.Name = nameof(ProductionRecord.PartNumber);
        Line.Editor.Name = nameof(ProductionRecord.Line);
        GoodCount.Editor.Name = nameof(ProductionRecord.GoodCount);
        BadCount.Editor.Name = nameof(ProductionRecord.BadCount);
        Remarks.Editor.Name = nameof(ProductionRecord.Remarks);

        FormFields = UiLayout.AutoGrid(WorkDate, PartNumber, Line, GoodCount, BadCount, Remarks);
        RecordsGrid = new DataGrid<ProductionRecord>().SetData(Records)
            .FormatColumn(nameof(ProductionRecord.WorkDate), "yyyy/MM/dd")
            .FormatColumn(nameof(ProductionRecord.GoodCount), "N0")
            .FormatColumn(nameof(ProductionRecord.BadCount), "N0");
        Search = new SearchPanel()
            .AddText(nameof(ProductionRecord.PartNumber), "品番（部分一致）")
            .AddText(nameof(ProductionRecord.Remarks), "備考（部分一致）")
            .AddSelect(nameof(ProductionRecord.Line), "ライン", SampleData.LineChoices)
            .AddDateRange(nameof(ProductionRecord.WorkDate), "作業日", DateTime.Today.AddDays(-7), DateTime.Today)
            .Connect(RecordsGrid);
        SaveButton = Elements.Button("登録", SaveRecord);
        AddFieldButton = Elements.Button("項目を追加", AddField);
        RemoveFieldButton = Elements.Button("追加項目を削除", RemoveField);
        RecordsGrid.ViewChanged += (_, _) => UpdateCount();

        SizeSelector = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 320,
            DataSource = RootSizes.Presets.ToArray()
        };
        var sizeLabel = Elements.Label("ルートサイズ・比率");
        sizeLabel.AutoSize = true;
        Screen = UiLayout.Root(this, RootSize.HdPlus,
            UiLayout.Wrap(sizeLabel, SizeSelector),
            Elements.Label("実績入力 — ウィンドウ幅に合わせて入力欄が折り返されます。"),
            FormFields,
            UiLayout.Wrap(SaveButton, Elements.Button("入力をクリア", ClearInput), AddFieldButton, RemoveFieldButton,
                Elements.Button("コンポーネント一覧", () => new ComponentGalleryForm().Show(this))),
            _status,
            Elements.Label("実績検索 — 列ヘッダーをクリックすると昇順 / 降順に並べ替えます。"),
            Search,
            _count,
            RecordsGrid.FillRemainingHeight());
        SizeSelector.SelectedIndexChanged += (_, _) =>
        {
            if (SizeSelector.SelectedItem is RootSizePreset preset) Screen.UseSize(preset.Id);
        };
        SizeSelector.SelectedItem = RootSizes.Presets.Single(preset => preset.Id == RootSize.HdPlus);
        Shown += (_, _) => SizeSelector.SelectedItem = RootSizes.Presets.Single(preset => preset.Id == Screen.Preset);
        UpdateCount();
    }

    private void SaveRecord()
    {
        if (!PartNumber.ValidateValue())
        {
            _status.Text = "入力内容を確認してください。";
            PartNumber.Editor.Focus();
            return;
        }
        Records.Add(new ProductionRecord
        {
            WorkDate = WorkDate.Value,
            PartNumber = PartNumber.Value.Trim(),
            Line = Line.Value,
            GoodCount = (int)GoodCount.Value,
            BadCount = (int)BadCount.Value,
            Remarks = Remarks.Value
        });
        RecordsGrid.RefreshData();
        _status.Text = $"{PartNumber.Value.Trim()} を登録しました。検索条件に一致する実績を一覧に表示します。";
        UpdateCount();
        PartNumber.Value = "";
        GoodCount.Value = 0;
        BadCount.Value = 0;
        Remarks.Value = "";
        PartNumber.Editor.Focus();
    }

    private void ClearInput()
    {
        WorkDate.Value = DateTime.Today;
        PartNumber.Value = "";
        Line.Value = "A";
        GoodCount.Value = 0;
        BadCount.Value = 0;
        Remarks.Value = "";
        PartNumber.ClearValidation();
        foreach (var field in _extraFields) field.Value = "";
        _status.Text = "入力をクリアしました。";
    }

    private void AddField()
    {
        var field = Field.Text($"追加項目 {_extraFields.Count + 1}").WithHelp("自動配置の確認用です。保存対象には含みません。");
        field.Editor.Name = $"ExtraField{_extraFields.Count + 1}";
        _extraFields.Add(field);
        FormFields.Add(field);
        _status.Text = "項目を追加しました。行数や座標の指定は不要です。";
    }

    private void RemoveField()
    {
        if (_extraFields.Count == 0) return;
        var field = _extraFields[^1];
        _extraFields.RemoveAt(_extraFields.Count - 1);
        FormFields.Controls.Remove(field);
        field.Dispose();
        _status.Text = "追加項目を削除しました。";
    }

    private void UpdateCount() => _count.Text = $"表示 {RecordsGrid.RowCount} 件 / 全 {Records.Count} 件";
}
