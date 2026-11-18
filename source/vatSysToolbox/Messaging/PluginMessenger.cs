using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace vatSysToolbox.Messaging;

/// <summary>
/// Sends messages to other plugins, and receives messages from other plugins.
/// </summary>
public interface IPluginMessenger
{
    /// <summary>
    /// Starts to receive the messages from the inbound queue. Messages that other plugins send before this call stay
    /// in the queue. Add the handlers before you call this method.
    /// </summary>
    void Start();

    /// <summary>
    /// Sends a message to a plugin. This method does not wait for the recipient to handle the message.
    /// </summary>
    /// <param name="recipient">The mailbox name of the plugin that receives the message.</param>
    /// <param name="type">The type of the message (for example, <c>dev.example.my-plugin.thing-changed</c>).</param>
    /// <param name="content">The data of the message. If <c>null</c>, the message has no data.</param>
    void Send(string recipient, string type, MessageContent? content = null);

    /// <summary>
    /// Sends a message to a plugin. The task completes when the message is in the inbound queue of the recipient.
    /// </summary>
    /// <param name="recipient">The mailbox name of the plugin that receives the message.</param>
    /// <param name="type">The type of the message (for example, <c>dev.example.my-plugin.thing-changed</c>).</param>
    /// <param name="content">The data of the message. If <c>null</c>, the message has no data.</param>
    /// <param name="cancellationToken">Cancels the send operation.</param>
    Task SendAsync(string recipient, string type, MessageContent? content = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to all the other plugins that have a messenger. This method does not wait for the recipients to
    /// handle the message.
    /// </summary>
    /// <param name="type">The type of the message (for example, <c>dev.example.my-plugin.thing-changed</c>).</param>
    /// <param name="content">The data of the message. If <c>null</c>, the message has no data.</param>
    void Broadcast(string type, MessageContent? content = null);

    /// <summary>
    /// Sends a message to all the other plugins that have a messenger.
    /// The task completes when the message is in the inbound queue of each recipient.
    /// </summary>
    /// <param name="type">The type of the message (for example, <c>dev.example.my-plugin.thing-changed</c>).</param>
    /// <param name="content">The data of the message. If <c>null</c>, the message has no data.</param>
    /// <param name="cancellationToken">Cancels the broadcast operation.</param>
    Task BroadcastAsync(string type, MessageContent? content = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request to one plugin, and waits for the reply.
    /// You must call <see cref="Start"/> before the reply can be received.
    /// </summary>
    /// <exception cref="MessagingException">The recipient could not handle the request.</exception>
    /// <param name="recipient">The mailbox name of the plugin that receives the request.</param>
    /// <param name="type">The type of the request (for example, <c>dev.example.my-plugin.get-thing</c>).</param>
    /// <param name="content">The data of the request. If <c>null</c>, the request has no data.</param>
    /// <param name="cancellationToken">Stops the wait for the reply.</param>
    /// <returns>The reply.</returns>
    Task<PluginMessage> RequestAsync(string recipient, string type, MessageContent? content = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a handler for the messages of a specified <paramref name="type"/>. A message can have more than one handler.
    /// Dispose the return value to unsubscribe.
    /// </summary>
    /// <param name="type">The type of the message (for example, <c>dev.example.my-plugin.thing-changed</c>).</param>
    /// <param name="handler">Handles the message. The cancellation token is canceled when the messenger is disposed.</param>
    IDisposable Subscribe(string type, Func<PluginMessage, CancellationToken, Task> handler);

    /// <summary>
    /// Adds a synchronous handler for the messages of a specified <paramref name="type"/>. A type can have more than one handler.
    /// Dispose the return value to unsubscribe.
    /// </summary>
    /// <param name="type">The type of the message (for example, <c>dev.example.my-plugin.thing-changed</c>).</param>
    /// <param name="handler">Handles the message.</param>
    IDisposable Subscribe(string type, Action<PluginMessage> handler);

    /// <summary>
    /// Adds a synchronous handler for the messages of a specified <paramref name="type"/>. Use this overload if the handler does not use the
    /// content of the message. A type can have more than one handler.
    /// Dispose the return value to unsubscribe.
    /// </summary>
    /// <param name="type">The type of the message (for example, <c>dev.example.my-plugin.thing-changed</c>).</param>
    /// <param name="handler">Handles the message.</param>
    IDisposable Subscribe(string type, Action handler);

    /// <summary>
    /// Adds the handler for the requests of a specified <paramref name="type"/>. A type can have only one request handler.
    /// Dispose the return value to unsubscribe.
    /// </summary>
    /// <exception cref="InvalidOperationException">The type already has a request handler.</exception>
    /// <param name="type">The type of the request (for example, <c>dev.example.my-plugin.get-thing</c>).</param>
    /// <param name="handler">
    /// Handles the request, and gives the content of the reply. If the handler throws an exception, the requester
    /// receives the error. The cancellation token is canceled when the messenger is disposed.
    /// </param>
    IDisposable Handle(string type, Func<PluginMessage, CancellationToken, Task<MessageContent>> handler);
}

/// <summary>
/// Sends messages to other plugins, and receives messages from other plugins.
/// </summary>
/// <remarks>
/// <para>
/// The messenger handles messages one at a time, in the sequence that they were received.
/// It handles requests at the same time as other messages and requests. Thus, two plugins can send requests to
/// each other from their handlers.
/// </para>
/// <para>
/// If a direct message or a request has no handler, the messenger logs a warning. The sender of a direct message is
/// not told. The requester receives an error reply, so that it does not wait forever. If a broadcast message has no
/// handler, the messenger ignores it.
/// </para>
/// </remarks>
public sealed class PluginMessenger : IPluginMessenger, IAsyncDisposable
{
    readonly string _mailboxName;
    readonly IMessageTransport _transport;
    readonly IErrorReporter _errorReporter;
    readonly ILogger<PluginMessenger> _logger;
    readonly IMailbox _mailbox;
    readonly BackgroundTask _receiver;

    readonly object _gate = new();
    readonly Dictionary<string, List<Func<PluginMessage, CancellationToken, Task>>> _messageHandlers = new();
    readonly Dictionary<string, Func<PluginMessage, CancellationToken, Task<MessageContent>>> _requestHandlers = new();
    readonly ConcurrentDictionary<string, TaskCompletionSource<PluginMessage>> _pendingRequests = new();

    /// <param name="mailboxName">
    /// The name of the inbound queue of this plugin. Other plugins use this name to send messages to this plugin.
    /// </param>
    /// <param name="transport">Moves the messages between the plugins.</param>
    /// <param name="errorReporter">Reports the errors that occur when the messenger receives or handles a message.</param>
    /// <param name="logger">
    /// Logs the messages that are sent and received, the broadcasts that have no handler, and the replies that have
    /// no request.
    /// </param>
    /// <exception cref="MessagingException">A different plugin uses the same mailbox name.</exception>
    public PluginMessenger(string mailboxName, IMessageTransport transport, IErrorReporter errorReporter, ILogger<PluginMessenger> logger)
    {
        _mailboxName = mailboxName;
        _transport = transport;
        _errorReporter = errorReporter;
        _logger = logger;
        _mailbox = transport.OpenMailbox(mailboxName);
        _receiver = new BackgroundTask(Receive);
    }

    /// <inheritdoc/>
    public void Start()
    {
        _receiver.Start();
    }

    /// <inheritdoc/>
    public void Send(string recipient, string type, MessageContent? content = null)
    {
        var envelope = CreateEnvelope(EnvelopeKind.Message, type, content);
        LogSending(envelope, recipient);
        _transport.Send(recipient, envelope);
    }

    /// <inheritdoc/>
    public Task SendAsync(string recipient, string type, MessageContent? content = null, CancellationToken cancellationToken = default)
    {
        var envelope = CreateEnvelope(EnvelopeKind.Message, type, content);
        LogSending(envelope, recipient);
        return _transport.SendAsync(recipient, envelope, cancellationToken);
    }

    /// <inheritdoc/>
    public void Broadcast(string type, MessageContent? content = null)
    {
        var envelope = CreateEnvelope(EnvelopeKind.Broadcast, type, content);
        LogSending(envelope, recipient: null);
        _transport.Broadcast(envelope);
    }

    /// <inheritdoc/>
    public Task BroadcastAsync(string type, MessageContent? content = null, CancellationToken cancellationToken = default)
    {
        var envelope = CreateEnvelope(EnvelopeKind.Broadcast, type, content);
        LogSending(envelope, recipient: null);
        return _transport.BroadcastAsync(envelope, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PluginMessage> RequestAsync(string recipient, string type, MessageContent? content = null, CancellationToken cancellationToken = default)
    {
        var envelope = CreateEnvelope(EnvelopeKind.Request, type, content);
        var reply = new TaskCompletionSource<PluginMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[envelope.Id] = reply;

        try
        {
            using (cancellationToken.Register(() => reply.TrySetCanceled(cancellationToken)))
            {
                LogSending(envelope, recipient);
                await _transport.SendAsync(recipient, envelope, cancellationToken).ConfigureAwait(false);
                return await reply.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            _pendingRequests.TryRemove(envelope.Id, out _);
        }
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(string type, Func<PluginMessage, CancellationToken, Task> handler)
    {
        lock (_gate)
        {
            if (!_messageHandlers.TryGetValue(type, out var handlers))
            {
                handlers = [];
                _messageHandlers[type] = handlers;
            }

            handlers.Add(handler);
        }

        return new Subscription(() =>
        {
            lock (_gate)
            {
                if (!_messageHandlers.TryGetValue(type, out var handlers))
                {
                    return;
                }

                handlers.Remove(handler);
                if (handlers.Count == 0)
                {
                    _messageHandlers.Remove(type);
                }
            }
        });
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(string type, Action<PluginMessage> handler)
    {
        return Subscribe(type, (message, _) =>
        {
            handler(message);
            return Task.CompletedTask;
        });
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(string type, Action handler)
    {
        return Subscribe(type, _ => handler());
    }

    /// <inheritdoc/>
    public IDisposable Handle(string type, Func<PluginMessage, CancellationToken, Task<MessageContent>> handler)
    {
        lock (_gate)
        {
            if (_requestHandlers.ContainsKey(type))
            {
                throw new InvalidOperationException($"The request type {type} already has a handler.");
            }

            _requestHandlers[type] = handler;
        }

        return new Subscription(() =>
        {
            lock (_gate)
            {
                _requestHandlers.Remove(type);
            }
        });
    }

    /// <summary>
    /// Stops the receipt of messages, and closes the inbound queue. The requests that wait for a reply are canceled.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _receiver.DisposeAsync().ConfigureAwait(false);
        _mailbox.Dispose();

        foreach (var pendingRequest in _pendingRequests.Values)
        {
            pendingRequest.TrySetCanceled();
        }
    }

    async Task Receive(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var envelope = await _mailbox.Receive(cancellationToken).ConfigureAwait(false);
                LogReceived(envelope);
                switch (envelope.Kind)
                {
                    case EnvelopeKind.Message:
                    case EnvelopeKind.Broadcast:
                        await HandleMessage(envelope, cancellationToken).ConfigureAwait(false);
                        break;

                    case EnvelopeKind.Request:
                        _ = Task.Run(() => HandleRequest(envelope, cancellationToken));
                        break;

                    case EnvelopeKind.Reply:
                        HandleReply(envelope);
                        break;
                    
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected when the messenger stops
                break;
            }
            catch (Exception ex)
            {
                _errorReporter.ReportError(ex);
            }
        }
    }

    async Task HandleMessage(Envelope envelope, CancellationToken cancellationToken)
    {
        Func<PluginMessage, CancellationToken, Task>[] handlers;
        lock (_gate)
        {
            handlers = _messageHandlers.TryGetValue(envelope.Type, out var registeredHandlers)
                ? registeredHandlers.ToArray()
                : [];
        }

        if (handlers.Length == 0)
        {
            if (envelope.Kind == EnvelopeKind.Broadcast)
            {
                _logger.LogDebug("No handler for the broadcast message {Type} from {Sender}", envelope.Type, envelope.Sender);
                return;
            }

            _logger.LogWarning("{Mailbox} has no handler for the message {Type} from {Sender}", _mailboxName, envelope.Type, envelope.Sender);
            return;
        }

        var message = ToPluginMessage(envelope);
        foreach (var handler in handlers)
        {
            try
            {
                await handler(message, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _errorReporter.ReportError(ex, $"Could not handle the message {envelope.Type} from {envelope.Sender}");
            }
        }
    }

    async Task HandleRequest(Envelope request, CancellationToken cancellationToken)
    {
        Envelope reply;
        try
        {
            Func<PluginMessage, CancellationToken, Task<MessageContent>>? handler;
            lock (_gate)
            {
                _requestHandlers.TryGetValue(request.Type, out handler);
            }

            if (handler is null)
            {
                // Log only. The requester still receives an error reply so that it does not wait forever.
                _logger.LogWarning("{Mailbox} has no handler for the request {Type} from {Sender}", _mailboxName, request.Type, request.Sender);
                reply = CreateReply(request, string.Empty, [], $"{_mailboxName} has no handler for the request {request.Type}.");
            }
            else
            {
                var content = await handler(ToPluginMessage(request), cancellationToken).ConfigureAwait(false);
                reply = CreateReply(request, content.ContentType, content.Payload, error: null);
            }
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex, $"Could not handle the request {request.Type} from {request.Sender}");
            reply = CreateReply(request, string.Empty, [], ex.Message);
        }

        try
        {
            LogSending(reply, request.Sender);
            await _transport.SendAsync(request.Sender, reply, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex, $"Could not send the reply for the request {request.Type} to {request.Sender}");
        }
    }

    void HandleReply(Envelope reply)
    {
        if (reply.CorrelationId is null || !_pendingRequests.TryRemove(reply.CorrelationId, out var pendingRequest))
        {
            // The request was canceled before the reply was received
            _logger.LogDebug("No pending request for the reply {Type} from {Sender}", reply.Type, reply.Sender);
            return;
        }

        if (reply.Error is not null)
        {
            pendingRequest.TrySetException(new MessagingException($"{reply.Sender} could not handle the request {reply.Type}. {reply.Error}"));
            return;
        }

        pendingRequest.TrySetResult(ToPluginMessage(reply));
    }

    void LogSending(Envelope envelope, string? recipient)
    {
        _logger.LogDebug(
            "{Mailbox} sending {Kind} {Type} ({Id}) to {Recipient}, {PayloadLength} bytes, correlation {CorrelationId}, error {Error}",
            _mailboxName,
            envelope.Kind,
            envelope.Type,
            envelope.Id,
            recipient ?? "all plugins",
            envelope.Payload.Length,
            envelope.CorrelationId ?? "none",
            envelope.Error ?? "none");
    }

    void LogReceived(Envelope envelope)
    {
        _logger.LogDebug(
            "{Mailbox} received {Kind} {Type} ({Id}) from {Sender}, {PayloadLength} bytes, correlation {CorrelationId}, error {Error}",
            _mailboxName,
            envelope.Kind,
            envelope.Type,
            envelope.Id,
            envelope.Sender,
            envelope.Payload.Length,
            envelope.CorrelationId ?? "none",
            envelope.Error ?? "none");
    }

    Envelope CreateEnvelope(EnvelopeKind kind, string type, MessageContent? content)
    {
        return new Envelope(
            Guid.NewGuid().ToString("N"),
            kind,
            _mailboxName,
            type,
            content?.ContentType ?? string.Empty,
            content?.Payload ?? []);
    }

    Envelope CreateReply(Envelope request, string contentType, byte[] payload, string? error)
    {
        return new Envelope(
            Guid.NewGuid().ToString("N"),
            EnvelopeKind.Reply,
            _mailboxName,
            request.Type,
            contentType,
            payload,
            request.Id,
            error);
    }

    static PluginMessage ToPluginMessage(Envelope envelope)
    {
        return new PluginMessage(envelope.Sender, envelope.Type, envelope.ContentType, envelope.Payload);
    }

    sealed class Subscription(Action unsubscribe) : IDisposable
    {
        Action? _unsubscribe = unsubscribe;

        public void Dispose()
        {
            Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
        }
    }
}
