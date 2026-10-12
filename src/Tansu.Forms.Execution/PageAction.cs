namespace Tansu.Forms.Execution;

/// <summary>Typed action adapters. Body validation runs before the supplied business handler.</summary>
public static class PageAction
{
    public static IPageAction Insert<T>(Func<T, ActionResponse> insert, string path = "/") where T : class
    {
        ArgumentNullException.ThrowIfNull(insert);
        return Insert<T>((input, _) => Task.FromResult(insert(input)), path);
    }
    public static IPageAction Insert<T>(Func<T, CancellationToken, Task<ActionResponse>> insert, string path = "/") where T : class
    {
        ArgumentNullException.ThrowIfNull(insert);
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/')) throw new ArgumentException("Path must start with '/'.", nameof(path));
        return new InsertAction<T>(insert, path);
    }
    private sealed class InsertAction<T>(Func<T, CancellationToken, Task<ActionResponse>> insert, string path) : IPageAction, IPositionalBodyAction where T : class
    {
        public object ParsePositionalBody(IReadOnlyList<string> values) => PositionalBody.Parse<T>(values);
        public Task<ActionResponse> ExecuteAsync(ActionRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (request.Path != path) return Task.FromResult(ActionResponse.NotFound());
            if (request.Method != ActionMethod.Insert) return Task.FromResult(ActionResponse.MethodNotAllowed());
            return insert(request.ReadBody<T>(), token);
        }
    }
}
