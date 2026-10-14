namespace Tansu.Forms.Execution;

/// <summary>Discoverable function names and positional input fields for CLI help.</summary>
public sealed record ActionFunctionInfo(string Name, IReadOnlyList<string> BodyFields, string? Description = null);

public interface IActionCatalog
{
    IReadOnlyList<ActionFunctionInfo> Functions { get; }
    object ParsePositionalBody(string function, IReadOnlyList<string> values);
}

/// <summary>A function subscription. Dispose to unregister; pass this object to GUI execution without repeating its name.</summary>
public class ActionSubscription : IDisposable, IPageAction, IActionCatalog
{
    private readonly PageAction _owner;
    internal ActionSubscription(PageAction owner, ActionFunctionInfo info,
        Func<ActionRequest, CancellationToken, Task<ActionResponse>> execute, Func<IReadOnlyList<string>, object>? parse)
    { _owner = owner; Info = info; Execute = execute; Parse = parse; }
    public string Name => Info.Name;
    public ActionFunctionInfo Info { get; }
    internal Func<ActionRequest, CancellationToken, Task<ActionResponse>> Execute { get; }
    internal Func<IReadOnlyList<string>, object>? Parse { get; }
    public IReadOnlyList<ActionFunctionInfo> Functions => _owner.IsSubscribed(this) ? [Info] : [];
    public object ParsePositionalBody(string function, IReadOnlyList<string> values)
        => _owner.IsSubscribed(this) && function.Equals(Name, StringComparison.OrdinalIgnoreCase) && Parse is { } parse
            ? parse(values) : throw new ActionInputException($"Function '{function}' does not accept positional body values.");
    public Task<ActionResponse> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!request.Function.Equals(Name, StringComparison.OrdinalIgnoreCase)
            || request.Subscription is { } expected && !ReferenceEquals(expected, this))
            return Task.FromResult(new ActionResponse(405, message: $"Function '{request.Function}' is not subscribed."));
        return _owner.ExecuteAsync(new ActionRequest(request.Function, request.Body, request.Path, request.Parameters)
            { Subscription = this }, cancellationToken);
    }
    public void Dispose() => _owner.Unsubscribe(this);
}

public sealed class ActionSubscription<T> : ActionSubscription where T : class
{
    internal ActionSubscription(PageAction owner, ActionFunctionInfo info,
        Func<ActionRequest, CancellationToken, Task<ActionResponse>> execute)
        : base(owner, info, execute, values => PositionalBody.Parse<T>(values)) { }
}

/// <summary>Registers arbitrary business functions. Shared body validation runs before each subscribed handler.</summary>
public sealed class PageAction : IPageAction, IActionCatalog
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ActionSubscription> _subscriptions = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<ActionFunctionInfo> Functions
    { get { lock (_gate) return _subscriptions.Values.Select(subscription => subscription.Info).ToArray(); } }

    /// <summary>Create a registry for multiple functions without explicitly constructing it.</summary>
    public static PageAction Create() => new();

    /// <summary>Create and subscribe one function. Pass the result directly to ActionApp, ActionClient or ToolRunner.</summary>
    public static ActionSubscription<T> Create<T>(Func<T, ActionResponse> handler, string? name = null, string? description = null) where T : class
        => Create().Subscribe(handler, name, description);
    public static ActionSubscription<T> Create<T>(Func<T, CancellationToken, Task<ActionResponse>> handler,
        string? name = null, string? description = null) where T : class
        => Create().Subscribe(handler, name, description);
    public static ActionSubscription<T> Create<T>(Func<T, Task<ActionResponse>> handler, string? name = null, string? description = null) where T : class
        => Create().Subscribe(handler, name, description);
    public static ActionSubscription Create(Func<ActionResponse> handler, string? name = null, string? description = null)
        => Create().Subscribe(handler, name, description);
    public static ActionSubscription Create(Func<CancellationToken, Task<ActionResponse>> handler, string? name = null, string? description = null)
        => Create().Subscribe(handler, name, description);
    public static ActionSubscription Create(Func<Task<ActionResponse>> handler, string? name = null, string? description = null)
        => Create().Subscribe(handler, name, description);

    public ActionSubscription<T> Subscribe<T>(Func<T, ActionResponse> handler, string? name = null, string? description = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Register<T>(handler, (input, _) => Task.FromResult(handler(input)), name, description);
    }
    public ActionSubscription<T> Subscribe<T>(Func<T, CancellationToken, Task<ActionResponse>> handler,
        string? name = null, string? description = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Register<T>(handler, handler, name, description);
    }
    public ActionSubscription<T> Subscribe<T>(Func<T, Task<ActionResponse>> handler, string? name = null, string? description = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Register<T>(handler, (input, _) => handler(input), name, description);
    }
    public ActionSubscription Subscribe(Func<ActionResponse> handler, string? name = null, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Register(handler, _ => Task.FromResult(handler()), name, description);
    }
    public ActionSubscription Subscribe(Func<CancellationToken, Task<ActionResponse>> handler, string? name = null, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Register(handler, handler, name, description);
    }
    public ActionSubscription Subscribe(Func<Task<ActionResponse>> handler, string? name = null, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Register(handler, _ => handler(), name, description);
    }
    private ActionSubscription<T> Register<T>(Delegate original, Func<T, CancellationToken, Task<ActionResponse>> handler,
        string? name, string? description) where T : class
    {
        var subscription = new ActionSubscription<T>(this,
            new(FunctionName(original, name), Array.AsReadOnly(PositionalBody.Names<T>().ToArray()), description),
            (request, token) => handler(request.ReadBody<T>(), token));
        Add(subscription); return subscription;
    }
    private ActionSubscription Register(Delegate original, Func<CancellationToken, Task<ActionResponse>> handler, string? name, string? description)
    {
        var subscription = new ActionSubscription(this, new(FunctionName(original, name), Array.Empty<string>(), description),
            (request, token) => request.Body is null ? handler(token)
                : Task.FromResult(ActionResponse.BadRequest($"'{request.Function}' takes no body.")), null);
        Add(subscription); return subscription;
    }
    private static string FunctionName(Delegate handler, string? name)
    {
        if (name is null)
        {
            name = handler.Method.Name;
            // Top-level local functions have compiler-generated method names, e.g. <<Main>$>g__Save|0_0.
            int start = name.IndexOf("g__", StringComparison.Ordinal), end = name.IndexOf('|');
            if (start >= 0 && end > start + 3) name = name[(start + 3)..end];
        }
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('-') || name.Any(character => char.IsWhiteSpace(character) || "<>|".Contains(character)))
            throw new ArgumentException("Supply a CLI function name for anonymous handlers (name: \"Save\").", nameof(name));
        return name;
    }
    private void Add(ActionSubscription subscription)
    {
        lock (_gate)
            if (!_subscriptions.TryAdd(subscription.Name, subscription))
                throw new ArgumentException($"Function '{subscription.Name}' is already subscribed.");
    }
    internal void Unsubscribe(ActionSubscription subscription)
    {
        lock (_gate)
            if (_subscriptions.TryGetValue(subscription.Name, out var current) && ReferenceEquals(current, subscription))
                _subscriptions.Remove(subscription.Name);
    }
    internal bool IsSubscribed(ActionSubscription subscription)
    {
        lock (_gate) return _subscriptions.TryGetValue(subscription.Name, out var current) && ReferenceEquals(current, subscription);
    }
    public object ParsePositionalBody(string function, IReadOnlyList<string> values)
    {
        ActionSubscription? subscription;
        lock (_gate) _subscriptions.TryGetValue(function, out subscription);
        return subscription?.Parse is { } parse ? parse(values)
            : throw new ActionInputException($"Function '{function}' does not accept positional body values.");
    }
    public Task<ActionResponse> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ActionSubscription? subscription;
        lock (_gate) _subscriptions.TryGetValue(request.Function, out subscription);
        if (subscription is null || request.Subscription is { } expected && !ReferenceEquals(expected, subscription))
            return Task.FromResult(new ActionResponse(405, message: $"Function '{request.Function}' is not subscribed."));
        return subscription.Execute(request, cancellationToken);
    }

    // Original API retained for existing callers.
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
    private sealed class InsertAction<T>(Func<T, CancellationToken, Task<ActionResponse>> insert, string path) : IPageAction, IPositionalBodyAction, IActionCatalog where T : class
    {
        public IReadOnlyList<ActionFunctionInfo> Functions => [new("Insert", Array.AsReadOnly(PositionalBody.Names<T>().ToArray()))];
        public object ParsePositionalBody(IReadOnlyList<string> values) => PositionalBody.Parse<T>(values);
        public object ParsePositionalBody(string function, IReadOnlyList<string> values)
            => function.Equals("Insert", StringComparison.OrdinalIgnoreCase) ? ParsePositionalBody(values)
                : throw new ActionInputException($"Unknown function '{function}'.");
        public Task<ActionResponse> ExecuteAsync(ActionRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (request.Path != path) return Task.FromResult(ActionResponse.NotFound());
            if (request.Method != ActionMethod.Insert) return Task.FromResult(ActionResponse.MethodNotAllowed());
            return insert(request.ReadBody<T>(), token);
        }
    }
}
