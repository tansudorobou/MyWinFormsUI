using System.Text;
using System.Text.Json;

namespace Tansu.Forms.Execution;

/// <summary>Runs one request, without starting a UI. Success body goes to stdout; structured errors go to stderr.</summary>
public sealed class ToolRunner
{
    private readonly IPageAction _action;
    public ToolRunner(IPageAction action) { ArgumentNullException.ThrowIfNull(action); _action = action; }
    public const string Usage = "SELECT|INSERT|UPDATE|DELETE [/<path>] [--id <id>] [--param name=value] [--body <json|value1 value2 ...|-> | --body-file <file>] [--envelope]";

    /// <summary>Exit codes: 0 success, 1 execution failure, 2 input/validation, 3 missing target, 4 unsupported method, 5 conflict, 130 cancellation.</summary>
    public async Task<int> RunAsync(IReadOnlyList<string> args, TextReader? input = null, TextWriter? output = null,
        TextWriter? error = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (input is null) Console.InputEncoding = new UTF8Encoding(false);
        if (output is null) Console.OutputEncoding = new UTF8Encoding(false);
        input ??= Console.In; output ??= Console.Out; error ??= Console.Error;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            if (args.Count == 1 && args[0] is "--help" or "-h")
            {
                await output.WriteLineAsync(Usage.AsMemory(), cancellation.Token).ConfigureAwait(false);
                return 0;
            }
            var options = Parse(args);
            object? body = null;
            if (options.Body is { } source)
            {
                if (source.Count == 1 && source[0] == "-")
                    body = ParseBody(await input.ReadToEndAsync(cancellation.Token).ConfigureAwait(false));
                else if (source.Count == 1 && (_action is not IPositionalBodyAction || IsJsonBody(source[0])))
                    body = ParseBody(source[0]);
                else if (_action is IPositionalBodyAction positional)
                    body = positional.ParsePositionalBody(source);
                else throw new ActionInputException("This action supports JSON bodies only.");
            }
            else if (options.BodyFile is { } file)
            {
                try { body = ParseBody(await File.ReadAllTextAsync(file, Encoding.UTF8, cancellation.Token).ConfigureAwait(false)); }
                catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException)
                { throw new ActionInputException($"Cannot read body file: {failure.Message}"); }
            }
            using var client = new ActionClient(_action);
            var response = await client.ExecuteAsync(new(options.Method, body, options.Path, options.Parameters), cancellation.Token).ConfigureAwait(false);
            if (!response.IsSuccess) return await WriteErrorAsync(response, error).ConfigureAwait(false);
            // Serialize before writing, so serialization failure cannot leave a partial JSON document on stdout.
            string result = JsonSerializer.Serialize(options.Envelope ? (object)response : response.Body, ActionJson.Options);
            await output.WriteLineAsync(result.AsMemory(), cancellation.Token).ConfigureAwait(false);
            return 0;
        }
        catch (ActionInputException failure)
        {
            return await WriteErrorAsync(ActionResponse.BadRequest(failure.Message), error).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            await WriteErrorAsync(new(499, message: "Canceled."), error).ConfigureAwait(false);
            return 130;
        }
        catch (Exception failure)
        {
            return await WriteErrorAsync(new(500, message: failure.Message), error).ConfigureAwait(false);
        }
        finally { Console.CancelKeyPress -= cancel; }
    }

    private static bool IsJsonBody(string text)
    {
        string trimmed = text.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[') || trimmed.StartsWith('"') || trimmed == "null";
    }
    private static JsonElement ParseBody(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ActionInputException("Body is empty. Supply one JSON document.");
        try { using var document = JsonDocument.Parse(json); return document.RootElement.Clone(); }
        catch (JsonException failure) { throw new ActionInputException($"Invalid JSON: {failure.Message}"); }
    }
    private static async Task<int> WriteErrorAsync(ActionResponse response, TextWriter error)
    {
        await error.WriteLineAsync(JsonSerializer.Serialize(new { response.StatusCode,
            Message = response.Message ?? "Action failed.", response.FieldErrors }, ActionJson.Options)).ConfigureAwait(false);
        return response.StatusCode switch { 400 or 422 => 2, 404 => 3, 405 => 4, 409 => 5, _ => 1 };
    }

    private sealed record Options(ActionMethod Method, string Path, Dictionary<string, string> Parameters, IReadOnlyList<string>? Body, string? BodyFile, bool Envelope);
    private static Options Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0) throw new ActionInputException($"Specify an operation. {Usage}");
        var method = args[0].ToUpperInvariant() switch
        {
            "SELECT" => ActionMethod.Select, "INSERT" => ActionMethod.Insert, "UPDATE" => ActionMethod.Update,
            "DELETE" => ActionMethod.Delete,
            _ => throw new ActionInputException($"Unknown operation '{args[0]}'. {Usage}")
        };
        string path = "/";
        List<string>? body = null;
        string? bodyFile = null;
        bool envelope = false, hasPath = false;
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string Value(ref int index)
        {
            if (++index >= args.Count) throw new ActionInputException($"Missing value for '{args[index - 1]}'.");
            return args[index];
        }
        for (int i = 1; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--path":
                    if (hasPath) throw new ActionInputException("Path was specified more than once.");
                    path = Value(ref i); hasPath = true; break;
                case "--id":
                    if (!parameters.TryAdd("id", Value(ref i))) throw new ActionInputException("Duplicate parameter 'id'.");
                    break;
                case "--param":
                    string entry = Value(ref i);
                    int separator = entry.IndexOf('=');
                    if (separator <= 0) throw new ActionInputException("Parameter must be name=value.");
                    if (!parameters.TryAdd(entry[..separator], entry[(separator + 1)..])) throw new ActionInputException("Duplicate parameter.");
                    break;
                case "--body":
                    if (body is not null || bodyFile is not null) throw new ActionInputException("Specify body once.");
                    body = [Value(ref i)];
                    if (body[0].StartsWith("--", StringComparison.Ordinal)) throw new ActionInputException("Missing body value.");
                    while (!IsJsonBody(body[0]) && body[0] != "-" && i + 1 < args.Count
                        && !args[i + 1].StartsWith("--", StringComparison.Ordinal)) body.Add(args[++i]);
                    if (body.Count > 1 && body[0] == "-") throw new ActionInputException("--body - must not be followed by body values.");
                    break;
                case "--body-file":
                    if (body is not null || bodyFile is not null) throw new ActionInputException("Specify body once.");
                    bodyFile = Value(ref i); break;
                case "--envelope":
                    if (envelope) throw new ActionInputException("Duplicate --envelope.");
                    envelope = true; break;
                default:
                    if (!hasPath && args[i].StartsWith('/')) { path = args[i]; hasPath = true; }
                    else throw new ActionInputException($"Unknown argument '{args[i]}'.");
                    break;
            }
        }
        if (!path.StartsWith('/')) throw new ActionInputException("Path must start with '/'.");
        if (method == ActionMethod.Select && (body is not null || bodyFile is not null))
            throw new ActionInputException("SELECT takes parameters, not a body.");
        return new(method, path, parameters, body, bodyFile, envelope);
    }
}
