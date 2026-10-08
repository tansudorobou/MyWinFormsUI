using System.Collections.ObjectModel;

namespace WinformsUI.Sample;

public static class SampleData
{
    public static List<GanttTask> CreateSchedule() =>
    [
        new() { Id = "material", Title = "材料の準備", Resource = "倉庫", Start = DateTime.Today.AddDays(-3), End = DateTime.Today.AddDays(-1), Progress = 100 },
        new() { Id = "cutting", Title = "切断", Resource = "ラインA", Start = DateTime.Today.AddDays(-1), End = DateTime.Today.AddDays(2), Progress = 60 },
        new() { Id = "machining", Title = "加工", Resource = "ラインB", Start = DateTime.Today.AddDays(2), End = DateTime.Today.AddDays(7), Progress = 20 },
        new() { Id = "assembly", Title = "組立", Resource = "ラインC", Start = DateTime.Today.AddDays(6), End = DateTime.Today.AddDays(11), Progress = 0 },
        new() { Id = "inspection", Title = "品質検査", Resource = "検査室", Start = DateTime.Today.AddDays(11), End = DateTime.Today.AddDays(13), Progress = 0 },
        new() { Id = "shipping", Title = "出荷", Resource = "倉庫", Start = DateTime.Today.AddDays(14), End = DateTime.Today.AddDays(14), Progress = 0 }
    ];
    public static IReadOnlyDictionary<string, string> LineChoices { get; } = new ReadOnlyDictionary<string, string>(
        new Dictionary<string, string> { ["A"] = "ラインA", ["B"] = "ラインB", ["C"] = "ラインC" });

    public static List<ProductionRecord> CreateRecords()
    {
        var records = new List<ProductionRecord>();
        for (int i = 0; i < 18; i++)
        {
            records.Add(new ProductionRecord
            {
                WorkDate = DateTime.Today.AddDays(-(i % 6)),
                PartNumber = i % 2 == 0 ? "P-102" : "P-205",
                Line = ((char)('A' + i % 3)).ToString(),
                GoodCount = 80 + i * 7,
                BadCount = i % 5,
                Remarks = i % 4 == 0 ? "ロット確認済み" : ""
            });
        }
        return records;
    }
}
