namespace Tansu.Forms.Execution;

/// <summary>Convenient verbs for a single page action. No UI context is captured; StateChanged can run on a worker thread.</summary>
public sealed class ActionClient : IDisposable
{
    private readonly IPageAction _action;
    private readonly object _gate = new();
    private CancellationTokenSource? _cancellation;
    private ActionRequest? _pending;
    private ActionResponse? _response;
    private Exception? _error;
    private bool _disposed, _canceled;

    public ActionClient(IPageAction action) { ArgumentNullException.ThrowIfNull(action); _action = action; }
    public bool IsRunning { get { lock (_gate) return _pending is not null; } }
    public ActionRequest? PendingRequest { get { lock (_gate) return _pending; } }
    public ActionResponse? LastResponse { get { lock (_gate) return _response; } }
    public Exception? LastError { get { lock (_gate) return _error; } }
    public bool IsCanceled { get { lock (_gate) return _canceled; } }
    public event EventHandler? StateChanged;

    public Task<ActionResponse> SelectAsync(IReadOnlyDictionary<string, string>? parameters = null, string path = "/",
        CancellationToken cancellationToken = default)
        => ExecuteAsync(new(ActionMethod.Select, path: path, parameters: parameters), cancellationToken);
    public Task<ActionResponse> InsertAsync(object? body = null, string path = "/", IReadOnlyDictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(new(ActionMethod.Insert, body, path, parameters), cancellationToken);
    public Task<ActionResponse> UpdateAsync(object? body = null, string path = "/", IReadOnlyDictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(new(ActionMethod.Update, body, path, parameters), cancellationToken);
    public Task<ActionResponse> DeleteAsync(IReadOnlyDictionary<string, string>? parameters = null, string path = "/",
        CancellationToken cancellationToken = default)
        => ExecuteAsync(new(ActionMethod.Delete, path: path, parameters: parameters), cancellationToken);

    public async Task<ActionResponse> ExecuteAsync(ActionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CancellationTokenSource cancellation;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_pending is not null) throw new InvalidOperationException("An action is already running on this page.");
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cancellation = cancellation; _pending = request; _response = null; _error = null; _canceled = false;
        }
        try
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
            cancellation.Token.ThrowIfCancellationRequested();
            ActionResponse response;
            // Handlers work with DTOs, never controls. Keep their awaits/CPU work off the UI context,
            // so closing a form cannot strand a cancellation continuation on its stopped message loop.
            try { response = await Task.Run(() => _action.ExecuteAsync(request, cancellation.Token), cancellation.Token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The action returned no response."); }
            catch (ActionInputException error)
            {
                response = new(error.FieldErrors.Count == 0 ? 400 : 422, message: error.Message,
                    fieldErrors: error.FieldErrors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal));
            }
            lock (_gate) _response = response;
            return response;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            lock (_gate) _canceled = true;
            throw;
        }
        catch (Exception error) { lock (_gate) _error = error; throw; }
        finally
        {
            lock (_gate) { _pending = null; _cancellation = null; }
            cancellation.Dispose();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Cancel()
    {
        CancellationTokenSource? cancellation;
        lock (_gate) cancellation = _cancellation;
        try { cancellation?.Cancel(); }
        catch (ObjectDisposedException) { }
    }
    public void Dispose()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; }
        Cancel();
    }
}
