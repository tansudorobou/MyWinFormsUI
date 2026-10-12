using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tansu.Forms.Execution;

public enum ActionMethod { Select, Insert, Update, Delete }

/// <summary>One page's UI-independent entry point. Implement Method/Path dispatch inside this method.</summary>
public interface IPageAction
{
    Task<ActionResponse> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken);
}

/// <summary>Body holds a .NET DTO in-process and a JsonElement when received from the CLI.</summary>
public sealed class ActionRequest
{
    public ActionRequest(ActionMethod method, object? body = null, string path = "/",
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        if (!Enum.IsDefined(method)) throw new ArgumentOutOfRangeException(nameof(method));
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/'))
            throw new ArgumentException("Path must start with '/'.", nameof(path));
        Method = method; Body = body; Path = path;
        Parameters = new ReadOnlyDictionary<string, string>(parameters is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase));
    }

    public ActionMethod Method { get; }
    public string Path { get; }
    /// <summary>Search conditions or target identifiers. The handler defines and validates their meaning.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }
    public object? Body { get; }

    /// <summary>Convert a DTO/JSON body and validate its DataAnnotations. Additional business rules belong in the action.</summary>
    public T ReadBody<T>() where T : class
    {
        T value;
        try { value = ActionJson.Read<T>(Body) ?? throw new ActionInputException("Body is required."); }
        catch (JsonException error) { throw new ActionInputException($"Invalid body: {error.Message}"); }
        var issues = new List<ValidationResult>();
        if (!Validator.TryValidateObject(value, new ValidationContext(value), issues, validateAllProperties: true))
        {
            var errors = issues.SelectMany(issue => (issue.MemberNames.Any() ? issue.MemberNames : [""])
                .Select(name => (Name: name, Message: issue.ErrorMessage ?? "Invalid value.")))
                .GroupBy(issue => issue.Name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(issue => issue.Message).ToArray(), StringComparer.Ordinal);
            throw new ActionInputException("Validation failed.", errors);
        }
        return value;
    }
}

/// <summary>Expected input failure; ActionClient converts it into a 400/422 response.</summary>
public sealed class ActionInputException : Exception
{
    public ActionInputException(string message, IReadOnlyDictionary<string, string[]>? fieldErrors = null) : base(message)
    {
        FieldErrors = ActionResponse.CopyErrors(fieldErrors);
    }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> FieldErrors { get; }
}

internal static class ActionJson
{
    internal static readonly JsonSerializerOptions Options = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
    internal static T? Read<T>(object? value) => value switch
    {
        null => default,
        T typed => typed,
        JsonElement json => json.Deserialize<T>(Options),
        _ => JsonSerializer.SerializeToElement(value, Options).Deserialize<T>(Options)
    };
}
