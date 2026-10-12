using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Tansu.Forms.Execution;

public sealed class ActionResponse
{
    public ActionResponse(int statusCode, object? body = null, string? message = null,
        IReadOnlyDictionary<string, string[]>? fieldErrors = null)
    {
        if (statusCode is < 100 or > 599) throw new ArgumentOutOfRangeException(nameof(statusCode));
        StatusCode = statusCode; Body = body; Message = message; FieldErrors = CopyErrors(fieldErrors);
    }
    public int StatusCode { get; }
    public object? Body { get; }
    public string? Message { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> FieldErrors { get; }
    [JsonIgnore] public bool IsSuccess => StatusCode is >= 200 and < 300;
    public T ReadBody<T>() => ActionJson.Read<T>(Body) ?? throw new InvalidOperationException("Response has no body.");

    public static ActionResponse Ok(object? body = null) => new(200, body);
    public static ActionResponse Created(object? body = null) => new(201, body);
    public static ActionResponse BadRequest(string message) => new(400, message: message);
    public static ActionResponse NotFound(string message = "Not found.") => new(404, message: message);
    public static ActionResponse MethodNotAllowed() => new(405, message: "Method not allowed.");
    public static ActionResponse ValidationFailed(IReadOnlyDictionary<string, string[]> errors,
        string message = "Validation failed.") => new(422, message: message, fieldErrors: errors);

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> CopyErrors(IReadOnlyDictionary<string, string[]>? errors)
        => new ReadOnlyDictionary<string, IReadOnlyList<string>>(errors is null
            ? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            : errors.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray()), StringComparer.Ordinal));
}
