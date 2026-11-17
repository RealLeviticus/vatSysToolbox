using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace vatSysToolbox.Messaging;

/// <summary>
/// Sends messages to other plugins, and receives messages from other plugins.
/// </summary>
public interface IPluginMessenger
{
    /// <summary>
    /// The name that other plugins use to send messages to this plugin.
    /// </summary>
    string MailboxName { get; }

    /// <summary>
    /// Starts to receive the messages from the inbound queue. Messages that other plugins send before this call stay
    /// in the queue. Add the handlers before you call this method.
    /// </summary>
    void Start();

    /// <summary>
    /// Sends a message to a plugin. This method does not wait for the recipient to handle the message.
    /// </summary>
    /// <param name="content">The data of the message. If <c>null</c>, the message has no data.</param>
    void Send(string recipient, string type, MessageContent? content = null);

    /// <summary>
    /// Sends a message to a plugin. The task completes when the message is in the inbound queue of the recipient.
    /// </summary>
    /// <param name="content">The data of the message. If <c>null</c>, the message has no data.</param>
    Task SendAsync(string recipient, string type, MessageContent? content = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to all the other plugins that have a messenger. This method does not wait for the recipients to
    /// handle the message.
    /// </summary>
    /// <param name="content">The data of the message. If <c>null</c>, the message has no data.</param>
    void Broadcast(string type, MessageContent? content = null);

    /// <summary>
    /// Sends a message to all the other plugins that have a messenger.
    /// The task completes when the message is in the inbound queue of each recipient.
    /// </summary>
    /// <param name="content">The data of the message. If <c>null</c>, the message has no data.</param>
    Task BroadcastAsync(string type, MessageContent? content = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request to one plugin, and waits for the reply.
    /// You must call <see cref="Start"/> before the reply can be received.
    /// </summary>
    /// <exception cref="MessagingException">The recipient could not handle the request.</exception>
    /// <param name="content">The data of the message. If <c>null</c>, the message has no data.</param>
    Task<PluginMessage> RequestAsync(string recipient, string type, MessageContent? content = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a handler for the messages of a specified <paramref name="type"/>. A message can have more than one handler.
    /// Dispose the return value to unsubscribe.
    /// </summary>
    IDisposable Subscribe(string type, Func<PluginMessage, CancellationToken, Task> handler);

    /// <summary>
    /// Adds a synchronous handler for the messages of a specified <paramref name="type"/>. A type can have more than one handler.
    /// Dispose the return value to unsubscribe.
    /// </summary>
    IDisposable Subscribe(string type, Action<PluginMessage> handler);

    /// <summary>
    /// Adds a synchronous handler for the messages of a specified <paramref name="type"/>. Use this overload if the handler does not use the
    /// content of the message. A type can have more than one handler.
    /// Dispose the return value to unsubscribe.
    /// </summary>
    IDisposable Subscribe(string type, Action handler);

    /// <summary>
    /// Adds the handler for the requests of a specified <paramref name="type"/>. A type can have only one request handler.
    /// Dispose the return value to unsubscribe.
    /// </summary>
    /// <exception cref="InvalidOperationException">The type already has a request handler.</exception>
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
/// If a direct message or a request has no handler, the messenger reports an error, and the requester receives the
/// error. If a broadcast message has no handler, the messenger ignores it.
/// </para>
/// </remarks>
public sealed class PluginMessenger : IPluginMessenger, IAsyncDisposable
{
    readonly IMessageTransport _transport;
    readonly IErrorReporter _errorReporter;
    readonly ILogger<PluginMessenger> _logger;
    readonly IMailbox _mailbox;
    readonly BackgroundTask _receiver;

    readonly object _gate = new();
    readonly Dictionary<string, List<Func<PluginMessage, CancellationToken, Task>>> _messageHandlers = new();
    readonly Dictionary<string, Func<PluginMessage, CancellationToken, Task<MessageContent>>> _requestHandlers = new();
    readonly ConcurrentDictionary<string, TaskCompletionSource<PluginMessage>> _pendingRequests = new();

    /// <exception cref="MessagingException">A different plugin uses the same mailbox name.</exception>
    public PluginMessenger(string mailboxName, IMessageTransport transport, IErrorReporter errorReporter, ILogger<PluginMessenger> logger)
    {
        MailboxName = mailboxName;
        _transport = transport;
        _errorReporter = errorReporter;
        _logger = logger;
        _mailbox = transport.OpenMailbox(mailboxName);
        _receiver = new BackgroundTask(Receive);
    }

    public string MailboxName { get; }

    public void Start()
    {
        _receiver.Start();
    }

    public void Send(string recipient, string type, MessageContent? content = null)
    {
        var envelope = CreateEnvelope(EnvelopeKind.Message, type, content);
        _transport.Send(recipient, envelope);
    }

    public Task SendAsync(string recipient, string type, MessageContent? content = null, CancellationToken cancellationToken = default)
    {
        var envelope = CreateEnvelope(EnvelopeKind.Message, type, content);
        return _transport.SendAsync(recipient, envelope, cancellationToken);
    }

    public void Broadcast(string type, MessageContent? content = null)
    {
        var envelope = CreateEnvelope(EnvelopeKind.Broadcast, type, content);
        _transport.Broadcast(envelope);
    }

    public Task BroadcastAsync(string type, MessageContent? content = null, CancellationToken cancellationToken = default)
    {
        var envelope = CreateEnvelope(EnvelopeKind.Broadcast, type, content);
        return _transport.BroadcastAsync(envelope, cancellationToken);
    }

    public async Task<PluginMessage> RequestAsync(string recipient, string type, MessageContent? content = null, CancellationToken cancellationToken = default)
    {
        var envelope = CreateEnvelope(EnvelopeKind.Request, type, content);
        var reply = new TaskCompletionSource<PluginMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[envelope.Id] = reply;

        try
        {
            using (cancellationToken.Register(() => reply.TrySetCanceled(cancellationToken)))
            {
                await _transport.SendAsync(recipient, envelope, cancellationToken).ConfigureAwait(false);
                return await reply.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            _pendingRequests.TryRemove(envelope.Id, out _);
        }
    }

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

    public IDisposable Subscribe(string type, Action<PluginMessage> handler)
    {
        return Subscribe(type, (message, _) =>
        {
            handler(message);
            return Task.CompletedTask;
        });
    }

    public IDisposable Subscribe(string type, Action handler)
    {
        return Subscribe(type, _ => handler());
    }

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

            throw new MessagingException($"{MailboxName} has no handler for the message {envelope.Type} from {envelope.Sender}.");
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
                throw new MessagingException($"{MailboxName} has no handler for the request {request.Type} from {request.Sender}.");

            var content = await handler(ToPluginMessage(request), cancellationToken).ConfigureAwait(false);
            reply = CreateReply(request, content.ContentType, content.Payload, error: null);
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex, $"Could not handle the request {request.Type} from {request.Sender}");
            reply = CreateReply(request, string.Empty, [], ex.Message);
        }

        try
        {
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

    Envelope CreateEnvelope(EnvelopeKind kind, string type, MessageContent? content)
    {
        return new Envelope(
            Guid.NewGuid().ToString("N"),
            kind,
            MailboxName,
            type,
            content?.ContentType ?? string.Empty,
            content?.Payload ?? []);
    }

    Envelope CreateReply(Envelope request, string contentType, byte[] payload, string? error)
    {
        return new Envelope(
            Guid.NewGuid().ToString("N"),
            EnvelopeKind.Reply,
            MailboxName,
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
