namespace WinformsUI;

/// <summary>Initial root/client-area sizes in pixels. All presets have a 16:9 aspect ratio.</summary>
public enum RootSize
{
    Compact,
    Hd,
    HdPlus,
    FullHd,
    Qhd,
    Uhd4K
}

public sealed record RootSizePreset(RootSize Id, string Name, int Width, int Height)
{
    public Size Size => new(Width, Height);
    public override string ToString() => $"{Name} ({Width} × {Height})";
}

public static class RootSizes
{
    public static IReadOnlyList<RootSizePreset> Presets { get; } = Array.AsReadOnly<RootSizePreset>(
    [
        new(RootSize.Compact, "コンパクト", 960, 540),
        new(RootSize.Hd, "HD", 1280, 720),
        new(RootSize.HdPlus, "HD+", 1600, 900),
        new(RootSize.FullHd, "Full HD", 1920, 1080),
        new(RootSize.Qhd, "QHD", 2560, 1440),
        new(RootSize.Uhd4K, "4K UHD", 3840, 2160)
    ]);

    public static Size GetSize(RootSize preset) => Presets.FirstOrDefault(item => item.Id == preset)?.Size
        ?? throw new ArgumentOutOfRangeException(nameof(preset), preset, "未定義のルートサイズです。");

    /// <summary>Keep the requested size, or shrink to a whole 16-by-9 unit that fits the available area.</summary>
    public static Size Fit(RootSize preset, Size availableClientSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(availableClientSize.Width, 16);
        ArgumentOutOfRangeException.ThrowIfLessThan(availableClientSize.Height, 9);
        Size requested = GetSize(preset);
        int units = Math.Min(requested.Width / 16, Math.Min(availableClientSize.Width / 16, availableClientSize.Height / 9));
        return new Size(units * 16, units * 9);
    }
}
