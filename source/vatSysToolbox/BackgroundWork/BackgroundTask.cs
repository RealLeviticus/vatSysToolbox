namespace vatSysToolbox.BackgroundWork;

/// <summary>
/// Runs a long-running worker that you can start and stop.
/// The worker must stop when its cancellation token is cancelled.
/// </summary>
/// <param name="worker">The work to do. The cancellation token is canceled when the task stops.</param>
public class BackgroundTask(Func<CancellationToken, Task> worker) : IAsyncDisposable
{
    bool IsDisposed { get; set; }

    CancellationTokenSource? _stoppingTokenSource;
    Task? _executingTask;

    /// <summary>
    /// <c>true</c> if the worker runs.
    /// </summary>
    public bool IsRunning => _executingTask is { IsCompleted: false };

    /// <summary>
    /// Starts the worker. If the worker runs, this method does nothing.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The task is disposed.</exception>
    public void Start()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(BackgroundTask));

        if (IsRunning)
            return;

        _stoppingTokenSource?.Dispose();
        _stoppingTokenSource = new CancellationTokenSource();
        _executingTask = worker(_stoppingTokenSource.Token);
    }

    /// <summary>
    /// Cancels the worker, and waits for it to stop. You can start the worker again after this.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The task is disposed.</exception>
    public async Task Stop()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(BackgroundTask));

        await StopInternal();
    }

    /// <summary>
    /// Stops the worker, and waits for it to stop.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (IsDisposed)
            return;

        await StopInternal();

        _executingTask?.Dispose();
        _stoppingTokenSource?.Dispose();
        IsDisposed = true;
    }

    async Task StopInternal()
    {
        if (_executingTask is null || _stoppingTokenSource is null)
            return;

        _stoppingTokenSource.Cancel();

        try
        {
            await _executingTask;
        }
        catch (OperationCanceledException) when (_stoppingTokenSource.IsCancellationRequested)
        {
            // Expected when the worker stops because of the cancellation
        }
    }
}
