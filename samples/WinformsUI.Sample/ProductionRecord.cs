using System.ComponentModel;

namespace WinformsUI.Sample;

/// <summary>Grid columns are generated from these properties; DisplayName defines their headers.</summary>
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

    [DisplayName("不良数")]
    public int BadCount { get; set; }

    [DisplayName("備考")]
    public string Remarks { get; set; } = "";
}
