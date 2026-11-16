using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace vatSysToolbox;

/// <summary>
/// Does the work items one at a time, in the sequence that they were added.
/// Work items that change the same state do not need a lock if they use the same queue.
/// </summary>
/// <remarks>
/// <para>
/// Each work item gets a cancellation token. The token is cancelled when the timeout of the work item ends, or when
/// the queue is disposed. A work item must give the token to all the operations that it waits for.
/// </para>
/// <para>
/// If a work item does not stop in the stop grace period after its timeout, the queue logs a warning. By default, the
/// queue continues to wait for the work item, thus the next work items cannot start. If the queue abandons timed out
/// work items, the queue starts the next work item, and the abandoned work item continues to run at the same time.
/// </para>
/// </remarks>
public class WorkQueue : IAsyncDisposable
{
    /// <summary>
    /// The timeout that the queue uses if the constructor does not get a timeout.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The stop grace period that the queue uses if the constructor does not get a stop grace period.
    /// </summary>
    public static readonly TimeSpan DefaultStopGracePeriod = TimeSpan.FromSeconds(5);

    readonly IErrorReporter _errorReporter;
    readonly ILogger<WorkQueue> _logger;
    readonly IClock _clock;
    readonly TimeSpan _defaultTimeout;
    readonly TimeSpan _stopGracePeriod;
    readonly bool _abandonTimedOutWork;
    readonly Channel<Func<CancellationToken, Task>> _workQueue;

    readonly CancellationTokenSource _cancellationTokenSource;
    readonly Task _worker;

    // The description of the work item that the worker waits for. DisposeAsync reads it on a different thread.
    volatile string? _currentWorkItem;

    static readonly TimeSpan WaitWarningThreshold = TimeSpan.FromSeconds(2);
    static readonly TimeSpan ExecutionWarningThreshold = TimeSpan.FromSeconds(5);

    /// <param name="errorReporter">Reports the exceptions from the work items.</param>
    /// <param name="logger">Logs the warnings for slow work items and work items that time out.</param>
    /// <param name="clock">Measures how long a work item waits in the queue.</param>
    /// <param name="defaultTimeout">
    /// The timeout for the work items that do not have a timeout. If this is <see langword="null"/>, the queue uses
    /// <see cref="DefaultTimeout"/>. Use <see cref="Timeout.InfiniteTimeSpan"/> for no timeout.
    /// </param>
    /// <param name="stopGracePeriod">
    /// The time that a cancelled work item has to stop, after its timeout or after the queue is disposed. If the work
    /// item does not stop in this time, the queue logs a warning. If this is <see langword="null"/>, the queue uses
    /// <see cref="DefaultStopGracePeriod"/>.
    /// </param>
    /// <param name="channelOptions">
    /// The options for the channel that keeps the work items. Use <see cref="BoundedChannelOptions"/> to set a maximum
    /// number of work items, or <see cref="UnboundedChannelOptions"/> for no maximum. If this is
    /// <see langword="null"/>, the channel has no maximum.
    /// </param>
    /// <param name="abandonTimedOutWork">
    /// If <see langword="true"/>, the queue does not wait for a work item that does not stop in the stop grace period
    /// after its timeout. The queue starts the next work item, and the abandoned work item continues to run at the same
    /// time. Thus, the work items can change the same state at the same time. Use this only if all the work items in
    /// the queue are safe to run at the same time.
    /// </param>
    public WorkQueue(
        IErrorReporter errorReporter,
        ILogger<WorkQueue> logger,
        IClock clock,
        TimeSpan? defaultTimeout = null,
        TimeSpan? stopGracePeriod = null,
        ChannelOptions? channelOptions = null,
        bool abandonTimedOutWork = false)
    {
        _errorReporter = errorReporter;
        _logger = logger;
        _clock = clock;
        _defaultTimeout = defaultTimeout ?? DefaultTimeout;
        _stopGracePeriod = stopGracePeriod ?? DefaultStopGracePeriod;
        _abandonTimedOutWork = abandonTimedOutWork;
        ValidateTimeout(_defaultTimeout, nameof(defaultTimeout));

        if (_stopGracePeriod < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(stopGracePeriod), _stopGracePeriod, "The stop grace period must be zero or more.");
        }

        _workQueue = channelOptions switch
        {
            null => Channel.CreateUnbounded<Func<CancellationToken, Task>>(),
            BoundedChannelOptions boundedChannelOptions => Channel.CreateBounded<Func<CancellationToken, Task>>(boundedChannelOptions),
            UnboundedChannelOptions unboundedChannelOptions => Channel.CreateUnbounded<Func<CancellationToken, Task>>(unboundedChannelOptions),
            _ => throw new ArgumentException($"The channel options type {channelOptions.GetType().Name} is not supported.", nameof(channelOptions))
        };

        _cancellationTokenSource = new CancellationTokenSource();
        _worker = Worker(_cancellationTokenSource.Token);
    }

    /// <summary>
    /// Adds a work item to the end of the queue.
    /// </summary>
    /// <param name="work">
    /// The work to do. The cancellation token is cancelled when the timeout ends, or when the queue is disposed.
    /// </param>
    /// <param name="timeout">
    /// The maximum time that the work item can run. If this is <see langword="null"/>, the queue uses its default
    /// timeout. Use <see cref="Timeout.InfiniteTimeSpan"/> for no timeout.
    /// </param>
    /// <param name="description">
    /// A short description of the work, for the log. For example, <c>"Open setup window"</c>. If this is
    /// <see langword="null"/>, the log shows the file name and the line number of the call.
    /// </param>
    /// <param name="callerFilePath">The file that calls this method. The compiler supplies this value.</param>
    /// <param name="callerLineNumber">The line that calls this method. The compiler supplies this value.</param>
    /// <returns>
    /// <see langword="true"/> if the queue accepted the work item. <see langword="false"/> if the queue did not accept
    /// it. For example, the queue is full, or it is disposed.
    /// </returns>
    public bool Enqueue(
        Func<CancellationToken, Task> work,
        TimeSpan? timeout = null,
        string? description = null,
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var itemTimeout = timeout ?? _defaultTimeout;
        ValidateTimeout(itemTimeout, nameof(timeout));

        // Use only the file name. The full path is the path on the build computer.
        var workItem = description ?? $"{Path.GetFileName(callerFilePath)}:{callerLineNumber}";
        var enqueueTime = _clock.UtcNow();
        return _workQueue.Writer.TryWrite(async shutdownToken =>
        {
            var waitTime = _clock.UtcNow() - enqueueTime;
            if (waitTime > WaitWarningThreshold)
            {
                _logger.LogWarning("[{WorkItem}] Work item waited {WaitMs}ms in queue", workItem, (long)waitTime.TotalMilliseconds);
            }

            using var timeoutSource = new CancellationTokenSource(itemTimeout);
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken, timeoutSource.Token);
            using var timeoutRegistration = timeoutSource.Token.Register(() => LogTimeout(workItem, itemTimeout));

            _currentWorkItem = workItem;
            var sw = Stopwatch.StartNew();
            try
            {
                var workTask = work(linkedSource.Token);
                if (!await StopsInTime(workTask, itemTimeout).ConfigureAwait(false))
                {
                    if (_abandonTimedOutWork)
                    {
                        _logger.LogWarning("[{WorkItem}] Work item did not stop {GracePeriodMs}ms after it was cancelled. The work will be abandoned to unblock the queue.", workItem, (long)_stopGracePeriod.TotalMilliseconds);
                        ReportErrorsFromAbandonedWork(workTask);
                        return;
                    }

                    _logger.LogWarning("[{WorkItem}] Work item did not stop {GracePeriodMs}ms after it was cancelled.", workItem, (long)_stopGracePeriod.TotalMilliseconds);
                }

                await workTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
            {
                // The timeout warning is already logged.
                return;
            }
            catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
            {
                // Expected during shutdown
                return;
            }
            catch (Exception ex)
            {
                SafelyReportError(ex);
            }
            finally
            {
                sw.Stop();
                _currentWorkItem = null;
            }

            if (sw.Elapsed > ExecutionWarningThreshold)
            {
                _logger.LogWarning("[{WorkItem}] Work item took {ElapsedMs}ms to complete", workItem, sw.ElapsedMilliseconds);
            }
        });
    }

    void LogTimeout(string workItem, TimeSpan timeout)
    {
        // This method runs on the timer thread. An exception on that thread stops vatSys.
        try
        {
            _logger.LogWarning("[{WorkItem}] Work item was cancelled because it did not complete in {TimeoutMs}ms", workItem, (long)timeout.TotalMilliseconds);
        }
        catch
        {
            // Ignore errors during logging
        }
    }

    /// <summary>
    /// Returns <see langword="false"/> if the work item does not complete in the timeout and the stop grace period.
    /// </summary>
    async Task<bool> StopsInTime(Task workTask, TimeSpan timeout)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
            return true;

        using var delaySource = new CancellationTokenSource();
        var delayTask = Task.Delay(timeout + _stopGracePeriod, delaySource.Token);
        var completedTask = await Task.WhenAny(workTask, delayTask).ConfigureAwait(false);
        delaySource.Cancel();

        return completedTask == workTask;
    }

    void ReportErrorsFromAbandonedWork(Task workTask)
    {
        // The worker does not wait for an abandoned work item, thus it does not see its exceptions.
        _ = workTask.ContinueWith(
            task =>
            {
                SafelyReportError(task.Exception!.GetBaseException());
            },
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
    }

    static void ValidateTimeout(TimeSpan timeout, string parameterName)
    {
        if (timeout <= TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(parameterName, timeout, "The timeout must be more than zero, or Timeout.InfiniteTimeSpan.");
        }
    }

    async Task Worker(CancellationToken shutdownToken)
    {
        while (!shutdownToken.IsCancellationRequested)
        {
            try
            {
                var work = await _workQueue.Reader.ReadAsync(shutdownToken).ConfigureAwait(false);
                await work(shutdownToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to execute work item.");
            }
        }
    }

    void SafelyReportError(Exception ex)
    {
        try
        {
            _errorReporter.ReportError(ex);
        }
        catch
        {
            // Ignored
        }
    }

    /// <summary>
    /// Cancels the current work item and stops the queue. The work items that did not start are not done.
    /// </summary>
    /// <remarks>
    /// If the current work item does not stop in the stop grace period, this method logs a warning and does not wait
    /// for it.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        // Cancel before the writer is completed. If the writer is completed first, a read that waits throws
        // ChannelClosedException, and not OperationCanceledException.
        _cancellationTokenSource.Cancel();
        _workQueue.Writer.TryComplete();

        using var delaySource = new CancellationTokenSource();
        var delayTask = Task.Delay(_stopGracePeriod, delaySource.Token);
        var completedTask = await Task.WhenAny(_worker, delayTask).ConfigureAwait(false);
        delaySource.Cancel();

        if (completedTask != _worker)
        {
            _logger.LogWarning("[{WorkItem}] Work item did not stop {GracePeriodMs}ms after the queue was disposed. The queue does not wait for it.", _currentWorkItem, (long)_stopGracePeriod.TotalMilliseconds);

            // Do not dispose the worker or the cancellation token source. The work item continues to use them.
            return;
        }

        _worker.Dispose();
        _cancellationTokenSource.Dispose();
    }
}
