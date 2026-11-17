namespace vatSysToolbox.Messaging;

/// <summary>
/// The kind of <see cref="Envelope"/>.
/// </summary>
public enum EnvelopeKind
{
    /// <summary>
    /// A message to one plugin.
    /// </summary>
    Message,

    /// <summary>
    /// A message to all the other plugins.
    /// </summary>
    Broadcast,

    /// <summary>
    /// A request to one plugin. The recipient sends a <see cref="Reply"/> to the sender.
    /// </summary>
    Request,

    /// <summary>
    /// The reply to a <see cref="Request"/>.
    /// </summary>
    Reply
}

/// <summary>
/// The data that a <see cref="IMessageTransport"/> moves between plugins.
/// </summary>
/// <param name="Id">The unique identifier of the envelope.</param>
/// <param name="Kind">The kind of the envelope.</param>
/// <param name="Sender">The mailbox name of the plugin that sends the envelope.</param>
/// <param name="Type">The type of the message (for example, <c>dev.example.my-plugin.thing-changed</c>).</param>
/// <param name="ContentType">The format of the payload (for example <c>application/json</c>). Empty if the message has no data.</param>
/// <param name="Payload">The data of the message. Empty if the message has no data.</param>
/// <param name="CorrelationId">For a <see cref="EnvelopeKind.Reply"/>, the <paramref name="Id"/> of the request.</param>
/// <param name="Error">For a <see cref="EnvelopeKind.Reply"/>, the error message if the recipient could not handle the request.</param>
public sealed record Envelope(
    string Id,
    EnvelopeKind Kind,
    string Sender,
    string Type,
    string ContentType,
    byte[] Payload,
    string? CorrelationId = null,
    string? Error = null);
