namespace WinformsUI;

public static class Ui
{
    public static Button Button(string text, Action onClick)
    {
        ArgumentNullException.ThrowIfNull(onClick);
        var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 3, 8, 3) };
        button.Click += (_, _) => onClick();
        return button;
    }

    public static Label Label(string text) => new() { Text = text, AutoSize = false, UseMnemonic = false, Margin = Padding.Empty };
}
