using System.Collections.ObjectModel;

namespace WinformsUI.Sample;

public static class SampleData
{
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
