using System.ComponentModel;
using System.Text.Json;

namespace Tansu.Forms.Execution;

/// <summary>Opt-in typed body conversion for CLI arguments after --body.</summary>
public interface IPositionalBodyAction
{
    object ParsePositionalBody(IReadOnlyList<string> values);
}

internal static class PositionalBody
{
    internal static JsonElement Parse<T>(IReadOnlyList<string> values) where T : class
    {
        var properties = ActionJson.Options.GetTypeInfo(typeof(T)).Properties
            .Where(property => property.Get is not null && property.Set is not null)
            .OrderBy(property => property.Order).ToArray();
        if (values.Count != properties.Length)
            throw new ActionInputException($"Body requires {properties.Length} values in this order: {string.Join(", ", properties.Select(property => property.Name))}.");
        var body = new Dictionary<string, object?>();
        for (int index = 0; index < properties.Length; index++)
        {
            var property = properties[index];
            Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            try
            {
                body[property.Name] = type == typeof(string) ? values[index]
                    : values[index].Length == 0 && Nullable.GetUnderlyingType(property.PropertyType) is not null ? null
                    : TypeDescriptor.GetConverter(type).ConvertFromInvariantString(values[index]);
            }
            catch (Exception error) when (error is FormatException or ArgumentException or NotSupportedException or OverflowException)
            { throw new ActionInputException($"Invalid value for '{property.Name}': {error.Message}"); }
        }
        return JsonSerializer.SerializeToElement(body, ActionJson.Options);
    }
}
