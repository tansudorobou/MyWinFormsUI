namespace WinformsUI;

public sealed record CommandItem(string Text, Action Execute, string Group = "", string Shortcut = "", bool Enabled = true);

/// <summary>Searchable actions. Enter executes, Escape closes; arrows navigate the native list.</summary>
public sealed class CommandPalette : Form
{
    private readonly List<CommandItem> _commands;
    private ThemeSession? _theme;
    public TextBox Query { get; } = new() { Dock = DockStyle.Top, PlaceholderText = "コマンドを検索" };
    public ListBox Results { get; } = new() { Dock = DockStyle.Fill };
    public CommandPalette(IEnumerable<CommandItem> commands)
    {
        _commands = commands.ToList(); Text = "コマンド"; ClientSize = new Size(480, 360);
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false; KeyPreview = true;
        Controls.AddRange([Results, Query]);
        Query.TextChanged += (_, _) => RefreshResults();
        Results.DoubleClick += (_, _) => ExecuteSelected();
        Query.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Down && Results.Items.Count > 0) { Results.Focus(); Results.SelectedIndex = Math.Max(0, Results.SelectedIndex); e.Handled = true; }
        };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ExecuteSelected(); }
            if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; Close(); }
        };
        Shown += (_, _) => Query.Focus();
        RefreshResults();
    }
    public IReadOnlyList<CommandItem> Matches => Results.Items.Cast<DisplayCommand>().Select(item => item.Command).ToArray();
    private void RefreshResults()
    {
        Results.BeginUpdate();
        try
        {
            Results.Items.Clear();
            foreach (var command in _commands.Where(command => command.Enabled &&
                         (command.Text + " " + command.Group + " " + command.Shortcut).Contains(Query.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)))
                Results.Items.Add(new DisplayCommand(command));
            if (Results.Items.Count > 0) Results.SelectedIndex = 0;
        }
        finally { Results.EndUpdate(); }
    }
    public bool ExecuteSelected()
    {
        if (Results.SelectedItem is not DisplayCommand command) return false;
        Hide(); command.Command.Execute(); Close(); return true;
    }
    private sealed record DisplayCommand(CommandItem Command)
    {
        public override string ToString() => $"{(Command.Group.Length > 0 ? Command.Group + " / " : "")}{Command.Text}  {Command.Shortcut}";
    }
    protected override void OnShown(EventArgs e)
    {
        if (Owner is not null) _theme ??= UiTheme.Inherit(this, Owner);
        base.OnShown(e);
    }
    protected override void Dispose(bool disposing) { if (disposing) _theme?.Dispose(); base.Dispose(disposing); }
}
