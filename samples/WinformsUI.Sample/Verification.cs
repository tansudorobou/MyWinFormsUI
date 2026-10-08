using System.ComponentModel;
using System.Drawing.Imaging;

namespace WinformsUI.Sample;

/// <summary>Run with --verify to exercise real native controls without a third project or test packages.</summary>
internal static class Verification
{
    internal static int Run()
    {
        string outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "verification");
        Directory.CreateDirectory(outputDirectory);
        var results = new List<string>();
        try
        {
            using var form = new SampleForm { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000) };
            form.Show();
            Settle(form);
            Check(RootSizes.Presets.Count == 6, "Six root-size presets are available");
            foreach (var preset in RootSizes.Presets)
            {
                Check(preset.Width * 9 == preset.Height * 16, "Every preset is exactly 16:9");
                form.SizeSelector.SelectedItem = preset;
                Settle(form);
                Check(form.Screen.Preset == preset.Id, $"The sample selector applies the root preset: requested={preset.Id}, actual={form.Screen.Preset}, selected={form.SizeSelector.SelectedItem}");
                Check(form.ClientSize.Width * 9 == form.ClientSize.Height * 16, "Applied client area remains 16:9");
                Check(form.ClientSize.Width <= preset.Width && form.ClientSize.Height <= preset.Height, "Presets are never enlarged by DPI scaling");
                Check(form.Size.Width <= System.Windows.Forms.Screen.FromControl(form).WorkingArea.Width
                    && form.Size.Height <= System.Windows.Forms.Screen.FromControl(form).WorkingArea.Height, "The window fits the monitor working area");
                CheckFields(form.FormFields);
            }
            Check(RootSizes.Fit(RootSize.FullHd, new Size(1000, 700)) == new Size(992, 558), "Oversized roots shrink without breaking 16:9");
            form.Screen.FitToWorkingArea = false;
            form.Screen.UseSize(RootSize.Compact);
            Settle(form);
            Check(form.ClientSize == new Size(960, 540), "Exact client sizing is available when fitting is disabled");
            form.Screen.FitToWorkingArea = true;
            form.SizeSelector.SelectedItem = RootSizes.Presets.Single(preset => preset.Id == RootSize.HdPlus);
            Settle(form);
            results.Add("PASS: Root presets, sample size selection, 16:9 client sizing and monitor fitting");
            VerifySelectPairs();
            Check(form.Line.Value == "A" && ((ComboBox)form.Line.Editor).Text == "ラインA", "Sample input displays the name and returns the line key");
            results.Add("PASS: Select key/value pairs, dictionary inputs, key filtering and clearing");
            Check(form.RecordsGrid.Grid.Columns.Count == 6, "Model properties automatically generate columns");
            Check(form.RecordsGrid.Grid.Columns[nameof(ProductionRecord.PartNumber)]!.HeaderText == "品番"
                && form.RecordsGrid.Grid.Columns[nameof(ProductionRecord.GoodCount)]!.HeaderText == "良品数",
                "DisplayName supplies model column headers");
            Check(ReferenceEquals(form.RecordsGrid.SelectedItem, form.Records[0]), "SelectedItem is the original model instance");
            Check((string?)form.RecordsGrid.Grid.Rows[0].Cells[nameof(ProductionRecord.PartNumber)].Value == form.Records[0].PartNumber,
                "Native DataGridView cells bind to model properties");
            results.Add("PASS: List binding, automatic model columns, DisplayName and typed selection");
            Check(form.FormFields.ColumnCount >= 3, "Wide form automatically uses multiple columns");
            CheckFields(form.FormFields);
            Check(form.Remarks.Width == form.FormFields.ClientSize.Width, "FullWidth spans the form");
            Capture(form, "wide.png");
            results.Add("PASS: Initial layout and label/editor composition");

            int initialHeight = form.FormFields.Height;
            form.AddFieldButton.PerformClick();
            Settle(form);
            Check(form.FormFields.Controls.Count == 7 && form.FormFields.Height > initialHeight, "Adding a field grows the form");
            CheckFields(form.FormFields);
            form.RemoveFieldButton.PerformClick();
            Settle(form);
            Check(form.FormFields.Controls.Count == 6 && form.FormFields.Height == initialHeight, "Removing a field shrinks the form");
            results.Add("PASS: Dynamic field addition and removal");

            Resize(760, 840);
            Settle(form);
            Check(form.FormFields.ColumnCount == 3, "Medium layout uses three columns, accounting for DPI");
            Resize(500, 840);
            Settle(form);
            Check(form.FormFields.ColumnCount == 2, "Narrow layout uses two columns");
            CheckFields(form.FormFields);
            Resize(390, 650);
            Settle(form);
            Check(form.FormFields.ColumnCount == 1, "Small layout uses one column");
            Check(form.Screen.VerticalScroll.Visible, "Small window enables scrolling");
            Check(!form.Screen.HorizontalScroll.Visible,
                $"Small window should not need horizontal scrolling: client={form.Screen.ClientSize}, display={form.Screen.DisplayRectangle}, children={string.Join("; ", form.Screen.Controls.Cast<Control>().Select(control => $"{control.GetType().Name}:{control.Bounds}"))}");
            CheckFields(form.FormFields);
            Capture(form, "narrow.png");
            form.ActiveControl = null;
            form.Screen.AutoScrollPosition = new Point(0, form.Screen.AutoScrollMinSize.Height);
            Settle(form);
            Check(form.RecordsGrid.Bounds.IntersectsWith(form.Screen.ClientRectangle), $"Scrolling reaches the grid: grid={form.RecordsGrid.Bounds}, viewport={form.Screen.ClientRectangle}, scroll={form.Screen.AutoScrollPosition}, min={form.Screen.AutoScrollMinSize}, display={form.Screen.DisplayRectangle}");
            form.Screen.AutoScrollPosition = Point.Empty;
            Resize(1060, 1040);
            Settle(form);
            Check(form.RecordsGrid.Height >= 160 && form.RecordsGrid.Bottom <= form.Screen.ClientSize.Height - form.Screen.Padding.Bottom,
                "Grid fills available vertical space without clipping");
            results.Add("PASS: Resize, automatic columns, scrolling and remaining-height grid");

            int originalCount = form.RecordsGrid.RowCount;
            form.SaveButton.PerformClick();
            Settle(form);
            Check(form.RecordsGrid.RowCount == originalCount && form.PartNumber.ErrorMessage is not null,
                "Required validation prevents an empty record");
            CheckFields(form.FormFields);
            form.PartNumber.Value = "P-NEW";
            form.GoodCount.Value = 42;
            form.SaveButton.PerformClick();
            Settle(form);
            Check(form.RecordsGrid.RowCount == originalCount + 1 && form.PartNumber.ErrorMessage is null,
                "A valid record is saved and validation cleared");
            Check(form.Records[^1].Line == "A", "Record creation stores the selected line key");
            results.Add("PASS: Required validation and record creation");

            var text = (InputField<string>)form.Search.Fields[0];
            var line = form.Search.Fields.OfType<InputField<string>>().Single(field => field.LabelText == "ライン");
            text.Value = "P-102";
            line.Value = "A";
            form.Search.SearchButton.PerformClick();
            Settle(form);
            Check(form.RecordsGrid.RowCount > 0, "Search finds rows");
            Check(((ComboBox)line.Editor).Text == "ラインA", "Search displays the choice label");
            Check(form.RecordsGrid.Items.All(row => row.PartNumber == "P-102" && row.Line == "A"),
                "Text and selection filters are combined");
            var originalOrder = form.Records.ToArray();
            form.RecordsGrid.Grid.Sort(form.RecordsGrid.Grid.Columns[nameof(ProductionRecord.GoodCount)]!, ListSortDirection.Descending);
            var values = form.RecordsGrid.Items.Select(row => row.GoodCount).ToArray();
            Check(values.SequenceEqual(values.OrderDescending()), "Native column sorting orders numeric values");
            Check(form.Records.SequenceEqual(originalOrder), "Sorting does not rearrange the caller's List");
            form.Search.ClearButton.PerformClick();
            Check(form.RecordsGrid.RowCount == originalCount + 1 && text.Value == "", "Clear restores all records");
            results.Add("PASS: Combined search, numeric sorting and clearing");

            const string specialPart = "P-%[*]O'Brien";
            var records = form.Records;
            records.Add(new ProductionRecord { WorkDate = DateTime.Today.AddHours(23).AddMinutes(59), PartNumber = specialPart, Line = "A", GoodCount = 10 });
            records.Add(new ProductionRecord { WorkDate = DateTime.Today.AddDays(1), PartNumber = specialPart, Line = "A", GoodCount = 20 });
            form.RecordsGrid.RefreshData();
            text.Value = specialPart;
            form.Search.SearchButton.PerformClick();
            Check(form.RecordsGrid.RowCount == 1 && form.RecordsGrid.Items[0].GoodCount == 10,
                "Quotes, brackets and wildcards are literal text; end date includes the whole day");
            var datesFields = form.Search.Fields.OfType<InputField<DateTime>>().ToArray();
            var start = datesFields[0];
            var end = datesFields[1];
            start.Value = end.Value.AddDays(1);
            Check(!form.Search.ApplySearch() && start.ErrorMessage is not null, "Invalid date range is rejected");
            form.Search.ClearSearch();
            results.Add("PASS: Literal text search, date boundaries and invalid range");

            // Each grid owns its display order/filter over the same original model instances.
            using var secondGrid = new DataGrid<ProductionRecord>().SetData(records);
            text.Value = "P-102";
            form.Search.ApplySearch();
            Check(secondGrid.RowCount == records.Count, "Independent grids do not share filtering state");
            results.Add("PASS: Independent list views");

            // Editing a search box alone must not change the last applied search on a refresh.
            text.Value = "P-205";
            var addedRecord = new ProductionRecord { WorkDate = DateTime.Today, PartNumber = "P-102", Line = "A", GoodCount = 500 };
            records.Add(addedRecord);
            form.RecordsGrid.RefreshData();
            Check(form.RecordsGrid.Items.All(record => record.PartNumber == "P-102")
                && ReferenceEquals(form.RecordsGrid.Items[0], addedRecord), "Refresh retains applied search and numeric sorting");
            records.Remove(addedRecord);
            form.RecordsGrid.RefreshData();
            Check(!form.RecordsGrid.Items.Contains(addedRecord), "Refresh reflects removal from the original List");
            form.Search.ClearSearch();
            form.RecordsGrid.Grid.Sort(form.RecordsGrid.Grid.Columns[nameof(ProductionRecord.WorkDate)]!, ListSortDirection.Ascending);
            var dates = form.RecordsGrid.Items.Select(record => record.WorkDate).ToArray();
            Check(dates.SequenceEqual(dates.Order()), "Date values are sorted chronologically");
            text.Value = "NO-MATCH";
            form.Search.ApplySearch();
            Check(form.RecordsGrid.RowCount == 0 && form.RecordsGrid.SelectedItem is null, "No matches leaves an empty result with no selected item");
            form.Search.ClearSearch();
            results.Add("PASS: Refresh preserves filter/sort, reflects removals and handles empty results");

            using var metadataGrid = new DataGrid<MetadataRecord>().SetData([]);
            Check(metadataGrid.RowCount == 0 && metadataGrid.Grid.Columns.Count == 3, "Empty lists still generate columns and honor Browsable(false)");
            Check(metadataGrid.Grid.Columns[nameof(MetadataRecord.Name)]!.HeaderText == "Name",
                "Properties without DisplayName fall back to their names");
            Check(metadataGrid.Grid.Columns[nameof(MetadataRecord.Enabled)] is DataGridViewCheckBoxColumn,
                "Boolean model properties use native checkbox columns");
            var metadataRecords = new List<MetadataRecord> { new() { Name = "A", Amount = null }, new() { Name = "B", Amount = 12 } };
            metadataGrid.SetData(metadataRecords);
            var bindingList = (IBindingList)((BindingSource)metadataGrid.Grid.DataSource!).List;
            bindingList.ApplySort(TypeDescriptor.GetProperties(typeof(MetadataRecord))[nameof(MetadataRecord.Amount)]!, ListSortDirection.Ascending);
            Check(ReferenceEquals(metadataGrid.Items[0], metadataRecords[0]), "Nullable numeric values sort without exceptions");
            bindingList.RemoveSort();
            Check(metadataGrid.Items.SequenceEqual(metadataRecords), "Removing the sort restores original list order");
            results.Add("PASS: Empty sources, metadata, nullable values and sort removal");

            int visibleHeight = form.FormFields.Height;
            form.Remarks.Visible = false;
            Settle(form);
            Check(form.FormFields.Height < visibleHeight, "Hiding a field removes its row");
            form.Remarks.Visible = true;
            Settle(form);
            Check(form.FormFields.Height == visibleHeight, "Showing a field restores its row");
            using var positiveNumber = Field.Number("正の数", min: 1);
            Check(positiveNumber.Value == 1, "Numeric fields default to their minimum");
            results.Add("PASS: Field visibility and numeric defaults");

            ExtendedVerification.Run(outputDirectory, results);

            form.Close();
            File.WriteAllLines(Path.Combine(outputDirectory, "results.txt"), results.Append("All checks passed."));
            return 0;

            void Resize(int width, int height) => form.ClientSize = new Size(
                (int)Math.Round(width * form.DeviceDpi / 96d), (int)Math.Round(height * form.DeviceDpi / 96d));

            void Capture(Form target, string name)
            {
                using var bitmap = new Bitmap(target.Width, target.Height);
                target.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(outputDirectory, name), ImageFormat.Png);
            }
        }
        catch (Exception exception)
        {
            File.WriteAllLines(Path.Combine(outputDirectory, "results.txt"), results.Append(exception.ToString()));
            return 1;
        }
    }

    private static void Settle(SampleForm form)
    {
        for (int i = 0; i < 3; i++) { form.PerformLayout(); Application.DoEvents(); }
    }

    private static void VerifySelectPairs()
    {
        using var field = Field.Select("選択", ("A", "同じ表示"), ("B", "同じ表示"));
        var editor = (ComboBox)field.Editor;
        int changes = 0;
        field.ValueChanged += (_, _) => changes++;
        field.Value = "B";
        Check(field.Value == "B" && editor.GetItemText(editor.SelectedItem) == "同じ表示", "Value setter selects a key even with duplicate labels");
        editor.SelectedIndex = 0;
        Check(field.Value == "A" && changes == 2, "Native selection changes return keys and notify listeners");
        using var dictionaryField = Field.Select("辞書", new Dictionary<string, string> { ["01"] = "第一工程" });
        Check(dictionaryField.Value == "01" && ((ComboBox)dictionaryField.Editor).GetItemText(((ComboBox)dictionaryField.Editor).SelectedItem) == "第一工程",
            "Dictionary choices display values and return keys");
        using var legacyField = Field.Select("互換", "選択A", "選択B");
        legacyField.Value = "選択B";
        Check(legacyField.Value == "選択B", "String-only choices remain usable");
        using var emptyField = Field.Select("空");
        Check(emptyField.Value == "", "Empty choice lists remain usable");

        using var grid = new DataGrid<SelectionRecord>().SetData(
            [new() { Code = "" }, new() { Code = "すべて" }, new() { Code = "A" }, new() { Code = "B" }]);
        using var search = new SearchPanel().AddSelect(nameof(SelectionRecord.Code), "コード",
            ("", "空のキー"), ("すべて", "通常のキー"), ("A", "共通表示"), ("B", "共通表示")).Connect(grid);
        var selection = (InputField<string>)search.Fields[0];
        Check(grid.RowCount == 4 && ((ComboBox)selection.Editor).SelectedIndex == 0, "Search begins with the distinct all option");
        Check(search.GetSelectedKey(nameof(SelectionRecord.Code)) is null, "All returns a null key");
        foreach (string key in new[] { "", "すべて", "A", "B" })
        {
            selection.Value = key;
            search.ApplySearch();
            Check(grid.RowCount == 1 && grid.Items[0].Code == key, "Search matches keys, including empty and all-named keys");
            Check(search.GetSelectedKey(nameof(SelectionRecord.Code)) == key, "Selected keys can be retrieved from the search panel");
        }
        search.ClearSearch();
        Check(grid.RowCount == 4 && ((ComboBox)selection.Editor).SelectedIndex == 0, "Clear resets the all option without treating it as a key");
        Check(search.GetSelectedKey(nameof(SelectionRecord.Code)) is null, "Cleared search returns a null key");

        bool rejectedDuplicate = false;
        try { using var invalid = Field.Select("重複", ("A", "表示1"), ("A", "表示2")); }
        catch (ArgumentException) { rejectedDuplicate = true; }
        Check(rejectedDuplicate, "Duplicate keys are rejected");
    }

    private static void CheckFields(AutoGridPanel panel)
    {
        var fields = panel.Controls.Cast<Control>().ToArray();
        foreach (var field in fields)
        {
            Check(field.Left >= 0 && field.Right <= panel.ClientSize.Width && field.Bottom <= panel.ClientSize.Height,
                "Fields fit inside their container");
            var editor = field.Controls.Cast<Control>().First(control => control is not Label);
            var label = field.Controls.Cast<Control>().First(control => control is Label);
            Check(label.Bottom <= editor.Top && editor.Right <= field.ClientSize.Width, "Label is above the fitted editor");
        }
        for (int i = 0; i < fields.Length; i++)
            for (int j = i + 1; j < fields.Length; j++)
                Check(!fields[i].Bounds.IntersectsWith(fields[j].Bounds), "Fields do not overlap");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class MetadataRecord
    {
        public string Name { get; set; } = "";
        public int? Amount { get; set; }
        public bool Enabled { get; set; }
        [Browsable(false)]
        public string InternalValue { get; set; } = "";
    }

    private sealed class SelectionRecord
    {
        [DisplayName("コード")]
        public string Code { get; set; } = "";
    }
}
