namespace vatSysToolbox.Messaging;

/// <summary>
/// Moves envelopes between plugins.
/// </summary>
public interface IMessageTransport
{
    /// <summary>
    /// Opens the inbound queue of a plugin. Only one plugin can open a queue with a given name.
    /// </summary>
    /// <exception cref="MessagingException">The queue is already open.</exception>
    IMailbox OpenMailbox(string mailboxName);

    /// <summary>
    /// Adds the envelope to the inbound queue of <paramref name="recipient"/>. If the recipient did not open its
    /// queue yet, the envelope stays in the queue until the recipient opens it.
    /// </summary>
    /// <param name="recipient">The mailbox name of the plugin that receives the envelope.</param>
    void Send(string recipient, Envelope envelope);

    /// <inheritdoc cref="Send"/>
    Task SendAsync(string recipient, Envelope envelope, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the envelope to the inbound queue of each plugin that has an open queue, except
    /// <see cref="Envelope.Sender"/>.
    /// </summary>
    void Broadcast(Envelope envelope);

    /// <inheritdoc cref="Broadcast"/>
    Task BroadcastAsync(Envelope envelope, CancellationToken cancellationToken);
}

/// <summary>
/// The inbound queue of a plugin. Dispose the mailbox to close the queue.
/// </summary>
public interface IMailbox : IDisposable
{
    string MailboxName { get; }

    /// <summary>
    /// Waits for the next envelope, and removes it from the queue.
    /// </summary>
    Task<Envelope> Receive(CancellationToken cancellationToken);
}
