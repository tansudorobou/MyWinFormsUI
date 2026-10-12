using Tansu.Forms.Execution;

namespace Tansu.Forms.Components;

public enum IpAddressVersion { Any, IPv4, IPv6 }

public static class InputValidation
{
    /// <summary>Use the same IP validator as model attributes and actions. Empty input is optional.</summary>
    public static InputField<string> IpAddress(this InputField<string> field,
        IpAddressVersion version = IpAddressVersion.Any, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (!Enum.IsDefined(version)) throw new ArgumentOutOfRangeException(nameof(version));
        var rule = new IpAddressAttribute((IpAddressKind)version);
        return field.ValidateWith(text => rule.IsValid(text), message ?? rule.FormatErrorMessage(field.LabelText));
    }
}
