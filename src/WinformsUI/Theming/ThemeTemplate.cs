using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinformsUI;

/// <summary>Serializable, application-wide design tokens. Null means keep the native default.</summary>
public sealed record ThemeTemplate
{
    public string Name { get; init; } = "Native";
    public string? FontFamily { get; init; }
    public float? FontSize { get; init; }
    public string? Background { get; init; }
    public string? Foreground { get; init; }
    public int? Gap { get; init; }
    public int? RootPadding { get; init; }
    public Dictionary<string, ThemeStyle> Roles { get; init; } = new(StringComparer.Ordinal);
    public string[] ChartColors { get; init; } = [];

    public static ThemeTemplate Native { get; } = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true, WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static ThemeTemplate Load(string path)
    {
        var template = JsonSerializer.Deserialize<ThemeTemplate>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("テーマが空です。");
        template.Validate();
        return template;
    }

    public void Save(string path)
    {
        Validate();
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("テーマ名を指定してください。");
        if (FontSize is <= 0 or > 96 || (FontSize is float size && !float.IsFinite(size))) throw new InvalidDataException("フォントサイズは0より大きく96以下にしてください。");
        if (Gap is < 0 || RootPadding is < 0) throw new InvalidDataException("余白を負数にはできません。");
        if (Roles is null || ChartColors is null) throw new InvalidDataException("RolesとChartColorsにはnullを指定できません。");
        _ = ParseColor(Background); _ = ParseColor(Foreground);
        foreach (var style in Roles.Values)
        {
            if (style is null) throw new InvalidDataException("スタイルが空です。");
            _ = ParseColor(style.Background); _ = ParseColor(style.Foreground);
            if (style.FontSize is <= 0 or > 96 || (style.FontSize is float roleSize && !float.IsFinite(roleSize))) throw new InvalidDataException("ロールのフォントサイズが無効です。");
        }
        foreach (string color in ChartColors) _ = ParseColor(color);
    }

    internal static Color? ParseColor(string? text)
    {
        if (text is null) return null;
        if (text.Length != 7 || text[0] != '#' || !int.TryParse(text.AsSpan(1), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out int value))
            throw new InvalidDataException($"色 '{text}' は #RRGGBB 形式で指定してください。");
        return Color.FromArgb((value >> 16) & 255, (value >> 8) & 255, value & 255);
    }
}

public sealed record ThemeStyle
{
    public string? Background { get; init; }
    public string? Foreground { get; init; }
    public string? FontFamily { get; init; }
    public float? FontSize { get; init; }
}

/// <summary>Optional hook for custom renderers such as charts.</summary>
public interface IThemeAware
{
    void ApplyTheme(ThemeTemplate template);
}
