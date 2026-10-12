#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:property OutputType=WinExe
#:property PublishAot=false
#:project ../../src/Tansu.Forms.Components/Tansu.Forms.Components.csproj

using System.ComponentModel;
using Tansu.Forms.Components;
using Tansu.Forms.Layout;
using FormLayout = Tansu.Forms.Layout.Layout;

FormsApp.Run(form =>
{
    form.Text = "製造実績 — Program.csだけのサンプル";
    var part = Field.Text("品番", "P-102").Required();
    var quantity = Field.Number("良品数", min: 0, initialValue: 100);
    var records = new List<ProductionRecord>
    {
        new() { PartNumber = "P-102", GoodCount = 120 },
        new() { PartNumber = "P-205", GoodCount = 80 }
    };
    var grid = new DataGrid<ProductionRecord>().SetData(records);
    var search = new SearchPanel().AddText(nameof(ProductionRecord.PartNumber), "品番検索").Connect(grid);
    var registerRow = FormLayout.Row(
        Elements.Button("登録", () =>
        {
            if (!part.ValidateValue()) return;
            records.Add(new() { PartNumber = part.Value, GoodCount = (int)quantity.Value });
            grid.RefreshData();
        }));
    registerRow.FlowDirection = FlowDirection.RightToLeft;

    var root = FormLayout.Root(form, RootSize.Workspace,
        FormLayout.AutoGrid(part, quantity),
        registerRow,
        search,
        grid.FillRemainingHeight());

    if (args.Contains("--verify"))
    {
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-10000, -10000);
        form.Shown += (_, _) => form.BeginInvoke((Action)(() =>
        {
            Environment.ExitCode = Thread.CurrentThread.GetApartmentState() == ApartmentState.STA
                && Application.OleRequired() == ApartmentState.STA && grid.RowCount == 2 && root.AutoMinimumSize ? 0 : 1;
            form.Close();
        }));
    }
});

public sealed class ProductionRecord
{
    [DisplayName("品番")]
    public string PartNumber { get; set; } = "";
    [DisplayName("良品数")]
    public int GoodCount { get; set; }
}
