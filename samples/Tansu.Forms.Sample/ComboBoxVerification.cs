namespace Tansu.Forms.Sample;

internal static class ComboBoxVerification
{
    internal static void Run(ICollection<string> results)
    {
        using var pairs = Elements.ComboBox((1, "同じ名前"), (2, "同じ名前"));
        Check(pairs.DropDownStyle == ComboBoxStyle.DropDownList && pairs.SelectedValue is 1, "Pairs configure selection and expose the first key before parenting");
        Check(pairs.AutoCompleteMode == AutoCompleteMode.None, "Duplicate labels disable ambiguous native auto-completion");
        Check(pairs.IntegralHeight && pairs.MaxDropDownItems == 12, "Native drop-down sizing uses whole rows and the configured item limit");
        pairs.SelectedValue = 2;
        Check(pairs.SelectedIndex == 1 && pairs.Text == "同じ名前", "Duplicate display names select by key");
        using var dictionary = Elements.ComboBox(new Dictionary<string, string> { ["A"] = "ラインA", ["B"] = "ラインB" }, selectedIndex: 1);
        Check(dictionary.SelectedValue as string == "B" && dictionary.Text == "ラインB", "Dictionary values are labels and keys are selected values");
        Check(dictionary.AutoCompleteSource == AutoCompleteSource.ListItems && dictionary.AutoCompleteMode == AutoCompleteMode.SuggestAppend, "Distinct labels enable native auto-completion");
        using var text = Elements.ComboBox("加工", "検査");
        Check(text.SelectedValue as string == "加工", "Strings work as both label and key");
        using var empty = Elements.ComboBox(Array.Empty<string>());
        Check(empty.Items.Count == 0 && empty.SelectedIndex == -1 && empty.SelectedValue is null, "Empty sources remain unselected");
        var models = new List<Line> { new(10, "第一工程"), new(20, "第二工程") };
        using var model = Elements.ComboBox(models, item => item.Name, item => item.Id, selectedIndex: -1);
        Check(model.SelectedIndex == -1, "Explicit initial no-selection is supported");
        model.SelectedValue = 20;
        Check(ReferenceEquals(model.SelectedItem, models[1]) && model.Text == "第二工程", "Models preserve SelectedItem identity and expose typed keys");
        using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000) };
        host.Controls.Add(model); host.Show(); Application.DoEvents();
        Check(model.SelectedValue is 20, "Parenting preserves the selected value");
        int changed = 0; model.SelectedValueChanged += (_, _) => changed++;
        model.SelectedIndex = 0;
        Check(model.SelectedValue is 10 && changed > 0, "Native selection events are retained");
        Reject<ArgumentException>(() => Elements.ComboBox((1, "A"), (1, "B")));
        Reject<ArgumentException>(() => Elements.ComboBox(("A", (string)null!)));
        Reject<ArgumentException>(() => Elements.ComboBox(((string)null!, "A")));
        Reject<ArgumentException>(() => Elements.ComboBox(models, item => item.Name.ToUpper(), item => item.Id));
        Reject<ArgumentOutOfRangeException>(() => Elements.ComboBox(models, item => item.Name, item => item.Id, selectedIndex: 2));
        host.Close();
        results.Add("PASS: Native ComboBox factories, strings/pairs/dictionaries/models, default/empty/no selection, key selection, model identity, native events and invalid data");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}"); }
    private sealed record Line(int Id, string Name);
}
