using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace Tansu.Forms.Execution;

public enum IpAddressKind { Any, IPv4, IPv6 }

/// <summary>Shared GUI/action IP validation. RequiredAttribute controls empty input.</summary>
public sealed class IpAddressAttribute : ValidationAttribute
{
    public IpAddressAttribute(IpAddressKind kind = IpAddressKind.Any)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
        ErrorMessage = kind == IpAddressKind.Any ? "IPv4またはIPv6のIPアドレスを入力してください。" : $"{kind}のIPアドレスを入力してください。";
    }
    public IpAddressKind Kind { get; }
    public override bool IsValid(object? value)
    {
        if (value is null || value is string blank && string.IsNullOrWhiteSpace(blank)) return true;
        if (value is not string raw) return false;
        string text = raw.Trim();
        if (text.Contains('[') || text.Contains(']') || !IPAddress.TryParse(text, out var address)) return false;
        if (address.AddressFamily == AddressFamily.InterNetwork) return Kind != IpAddressKind.IPv6 && DottedQuad(text);
        if (address.AddressFamily != AddressFamily.InterNetworkV6 || Kind == IpAddressKind.IPv4) return false;
        if (!text.Contains('.')) return true;
        string suffix = text[(text.LastIndexOf(':') + 1)..];
        int scope = suffix.IndexOf('%');
        return DottedQuad(scope >= 0 ? suffix[..scope] : suffix);
    }
    private static bool DottedQuad(string text) => text.Split('.') is { Length: 4 } parts && parts.All(part =>
        part.Length is >= 1 and <= 3 && !(part.Length > 1 && part[0] == '0') && part.All(char.IsAsciiDigit)
        && byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _));
}

/// <summary>One public static string-keyed dictionary supplies both ComboBox labels and allowed keys.</summary>
public sealed class ChoicesAttribute(Type provider, string propertyName) : ValidationAttribute
{
    public IReadOnlyDictionary<string, string> GetChoices()
    {
        var property = provider.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
        if (property?.GetValue(null) is not IReadOnlyDictionary<string, string> choices)
            throw new InvalidOperationException($"{provider.Name}.{propertyName} must be a public static IReadOnlyDictionary<string, string>.");
        return choices;
    }
    public override bool IsValid(object? value) => value is null || value is string text
        && (string.IsNullOrWhiteSpace(text) || GetChoices().ContainsKey(text));
    public override string FormatErrorMessage(string name) => ErrorMessage is null ? $"{name}の選択肢を確認してください。" : base.FormatErrorMessage(name);
}
