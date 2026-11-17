namespace vatSysToolbox.Messaging;

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

    Request,
    Reply
}

/// <summary>
/// The data that a <see cref="IMessageTransport"/> moves between plugins.
/// </summary>
/// <param name="Id">The unique identifier of the envelope.</param>
/// <param name="Sender">The mailbox name of the plugin that sends the envelope.</param>
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
