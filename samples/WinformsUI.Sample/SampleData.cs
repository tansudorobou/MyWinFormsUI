namespace WinformsUI.Sample;

public static class SampleData
{
    public static List<ProductionRecord> CreateRecords()
    {
        var records = new List<ProductionRecord>();
        for (int i = 0; i < 18; i++)
        {
            records.Add(new ProductionRecord
            {
                WorkDate = DateTime.Today.AddDays(-(i % 6)),
                PartNumber = i % 2 == 0 ? "P-102" : "P-205",
                Line = $"ライン{(char)('A' + i % 3)}",
                GoodCount = 80 + i * 7,
                BadCount = i % 5,
                Remarks = i % 4 == 0 ? "ロット確認済み" : ""
            });
        }
        return records;
    }
}
