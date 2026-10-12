namespace Tansu.Forms.Layout;

/// <summary>Initial client-area sizes in pixels, up to Full HD, for common desktop window shapes.</summary>
public enum RootSize
{
    Compact,
    Hd,
    HdPlus,
    FullHd,
    Small = 6,
    Standard,
    StandardLarge,
    Wide,
    WideLarge,
    Workspace,
    Square,
    Portrait
}

public sealed record RootSizePreset(RootSize Id, string Name, int Width, int Height)
{
    public Size Size => new(Width, Height);
    public int RatioWidth => Width / GreatestCommonDivisor(Width, Height);
    public int RatioHeight => Height / GreatestCommonDivisor(Width, Height);
    public string AspectRatio => $"{RatioWidth}:{RatioHeight}";
    /// <summary>Recommended minimum content size in logical pixels at 96 DPI.</summary>
    public Size MinimumClientSize => Height > Width ? new(480, 640)
        : Width == Height ? new(480, 480) : Width < 800 ? new(480, 360) : new(640, 480);
    public override string ToString() => $"{Name} ({Width} × {Height}, {AspectRatio})";

    private static int GreatestCommonDivisor(int a, int b)
    {
        while (b != 0) (a, b) = (b, a % b);
        return a;
    }
}

public static class RootSizes
{
    public static IReadOnlyList<RootSizePreset> Presets { get; } = Array.AsReadOnly<RootSizePreset>(
    [
        new(RootSize.Compact, "コンパクト", 960, 540),
        new(RootSize.Hd, "HD", 1280, 720),
        new(RootSize.HdPlus, "HD+", 1600, 900),
        new(RootSize.FullHd, "Full HD", 1920, 1080),
        new(RootSize.Small, "小さいダイアログ", 640, 480),
        new(RootSize.Standard, "標準フォーム", 800, 600),
        new(RootSize.StandardLarge, "大きい標準フォーム", 1024, 768),
        new(RootSize.Wide, "ワイドフォーム", 1280, 800),
        new(RootSize.WideLarge, "大きいワイドフォーム", 1440, 900),
        new(RootSize.Workspace, "作業画面", 1200, 800),
        new(RootSize.Square, "正方形", 800, 800),
        new(RootSize.Portrait, "縦長の入力フォーム", 600, 800)
    ]);

    public static RootSizePreset GetPreset(RootSize preset) => Presets.FirstOrDefault(item => item.Id == preset)
        ?? throw new ArgumentOutOfRangeException(nameof(preset), preset, "未定義のルートサイズです。");
    public static Size GetSize(RootSize preset) => GetPreset(preset).Size;
    public static Size GetMinimumClientSize(RootSize preset) => GetPreset(preset).MinimumClientSize;

    /// <summary>Keep the requested size, or shrink by whole aspect-ratio units to fit the available area.</summary>
    public static Size Fit(RootSize preset, Size availableClientSize)
    {
        var definition = GetPreset(preset);
        ArgumentOutOfRangeException.ThrowIfLessThan(availableClientSize.Width, definition.RatioWidth);
        ArgumentOutOfRangeException.ThrowIfLessThan(availableClientSize.Height, definition.RatioHeight);
        int units = Math.Min(definition.Width / definition.RatioWidth,
            Math.Min(availableClientSize.Width / definition.RatioWidth, availableClientSize.Height / definition.RatioHeight));
        return new Size(units * definition.RatioWidth, units * definition.RatioHeight);
    }
}
