using System.ComponentModel;
using System.Windows.Input;

namespace Tansu.Forms.Components;

public sealed class CommandFailedEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}

/// <summary>An awaitable UI command with single execution, cancellation and observable failures.</summary>
public sealed class AsyncCommand : ICommand, INotifyPropertyChanged, IDisposable
{
    private readonly Func<object?, CancellationToken, Task> _execute;
    private readonly Predicate<object?>? _canExecute;
    private readonly object _gate = new();
    private SynchronizationContext? _context = SynchronizationContext.Current;
    private int _contextThread = Environment.CurrentManagedThreadId;
    private CancellationTokenSource? _cancellation;
    private bool _running, _disposed, _canceled;
    private Exception? _lastError;

    public AsyncCommand(Func<CancellationToken, Task> execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = (_, token) => execute(token);
        _canExecute = canExecute is null ? null : _ => canExecute();
    }

    public AsyncCommand(Func<object?, CancellationToken, Task> execute, Predicate<object?>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute; _canExecute = canExecute;
    }

    public bool IsRunning { get { lock (_gate) return _running; } }
    public bool CanBeCanceled { get { lock (_gate) return _cancellation is { IsCancellationRequested: false }; } }
    public bool IsCanceled { get { lock (_gate) return _canceled; } }
    public Exception? LastError { get { lock (_gate) return _lastError; } }
    public event EventHandler? CanExecuteChanged;
    public event EventHandler? StateChanged;
    public event EventHandler<CommandFailedEventArgs>? ExecutionFailed;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool CanExecute(object? parameter)
    {
        lock (_gate) if (_disposed || _running) return false;
        return _canExecute?.Invoke(parameter) ?? true;
    }

    /// <summary>Returns false if unavailable. Execution failures and cancellation propagate to the awaiter.</summary>
    public Task<bool> ExecuteAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        // A closed UI no longer pumps its context; rejected requests must not be posted there.
        lock (_gate) if (_disposed || _running) return Task.FromResult(false);
        if (_context is { } context)
        {
            if (Environment.CurrentManagedThreadId != _contextThread)
            {
                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                context.Post(async _ =>
                {
                    try { completion.TrySetResult(await ExecuteCoreAsync(parameter, cancellationToken)); }
                    catch (OperationCanceledException error) { completion.TrySetCanceled(error.CancellationToken); }
                    catch (Exception error) { completion.TrySetException(error); }
                }, null);
                return completion.Task;
            }
            // WinForms can temporarily restore a different context during DoEvents/startup.
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(context);
            try { return ExecuteCoreAsync(parameter, cancellationToken); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }
        return ExecuteCoreAsync(parameter, cancellationToken);
    }

    private async Task<bool> ExecuteCoreAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (!CanExecute(parameter)) return false;
        CancellationTokenSource cancellation;
        lock (_gate)
        {
            if (_disposed || _running) return false;
            if (_context is null) { _context = SynchronizationContext.Current; _contextThread = Environment.CurrentManagedThreadId; }
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cancellation = cancellation; _running = true; _canceled = false; _lastError = null;
        }
        try
        {
            PublishState();
            cancellation.Token.ThrowIfCancellationRequested();
            await _execute(parameter, cancellation.Token);
            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            lock (_gate) _canceled = true;
            throw;
        }
        catch (Exception error)
        {
            lock (_gate) _lastError = error;
            Dispatch(() => ExecutionFailed?.Invoke(this, new CommandFailedEventArgs(error)));
            throw;
        }
        finally
        {
            lock (_gate) { _running = false; _cancellation = null; }
            cancellation.Dispose(); PublishState();
        }
    }

    // ICommand is a void UI entry point. The error remains available through LastError/ExecutionFailed.
    public async void Execute(object? parameter)
    {
        try { await ExecuteAsync(parameter); }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }

    public void NotifyCanExecuteChanged() => Dispatch(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
    public void Cancel()
    {
        CancellationTokenSource? cancellation;
        lock (_gate) cancellation = _cancellation;
        try { cancellation?.Cancel(); }
        catch (ObjectDisposedException) { } // The operation may have completed while cancellation was requested.
        PublishState();
    }

    private void PublishState() => Dispatch(() =>
    {
        foreach (string name in new[] { nameof(IsRunning), nameof(CanBeCanceled), nameof(IsCanceled), nameof(LastError) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        StateChanged?.Invoke(this, EventArgs.Empty);
    });

    private void Dispatch(Action action)
    {
        if (_context is { } context && Environment.CurrentManagedThreadId != _contextThread) context.Post(_ => action(), null);
        else action();
    }

    public void Dispose()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; }
        Cancel();
    }
}
