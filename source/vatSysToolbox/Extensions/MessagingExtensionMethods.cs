using vatSysToolbox.Messaging;

namespace vatSysToolbox.Extensions;

public static class MessagingExtensionMethods
{
    /// <summary>
    /// Converts the value to the content of a message.
    /// </summary>
    /// <param name="serializer">Converts the value to the payload.</param>
    /// <param name="value">The data of the message.</param>
    public static MessageContent ToContent<T>(this IMessageSerializer serializer, T value)
    {
        return new MessageContent(serializer.ContentType, serializer.Serialize(value));
    }

    /// <summary>
    /// Sends a message with the value to a plugin. See <see cref="IPluginMessenger.SendAsync"/>.
    /// </summary>
    /// <param name="messenger">The messenger that sends the message.</param>
    /// <param name="recipient">The mailbox name of the plugin that receives the message.</param>
    /// <param name="type">The type of the message (for example, <c>dev.example.my-plugin.thing-changed</c>).</param>
    /// <param name="value">The data of the message.</param>
    /// <param name="serializer">Converts the value to the payload.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    public static Task SendAsync<T>(
        this IPluginMessenger messenger,
        string recipient,
        string type,
        T value,
        IMessageSerializer serializer,
        CancellationToken cancellationToken = default)
    {
        return messenger.SendAsync(recipient, type, serializer.ToContent(value), cancellationToken);
    }

    /// <summary>
    /// Sends a message with the value to all the other plugins. See <see cref="IPluginMessenger.BroadcastAsync"/>.
    /// </summary>
    /// <param name="messenger">The messenger that sends the message.</param>
    /// <param name="type">The type of the message (for example, <c>dev.example.my-plugin.thing-changed</c>).</param>
    /// <param name="value">The data of the message.</param>
    /// <param name="serializer">Converts the value to the payload.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    public static Task BroadcastAsync<T>(
        this IPluginMessenger messenger,
        string type,
        T value,
        IMessageSerializer serializer,
        CancellationToken cancellationToken = default)
    {
        return messenger.BroadcastAsync(type, serializer.ToContent(value), cancellationToken);
    }

    /// <summary>
    /// Sends a request with the value to a plugin, and waits for the reply. See
    /// <see cref="IPluginMessenger.RequestAsync"/>.
    /// </summary>
    /// <param name="messenger">The messenger that sends the request.</param>
    /// <param name="recipient">The mailbox name of the plugin that receives the request.</param>
    /// <param name="type">The type of the request (for example, <c>dev.example.my-plugin.get-thing</c>).</param>
    /// <param name="value">The data of the request.</param>
    /// <param name="serializer">Converts the value to the payload.</param>
    /// <param name="cancellationToken">Stops the wait for the reply.</param>
    public static Task<PluginMessage> RequestAsync<T>(
        this IPluginMessenger messenger,
        string recipient,
        string type,
        T value,
        IMessageSerializer serializer,
        CancellationToken cancellationToken = default)
    {
        return messenger.RequestAsync(recipient, type, serializer.ToContent(value), cancellationToken);
    }

    /// <summary>
    /// Converts the payload with the serializer that has the same <see cref="PluginMessage.ContentType"/>.
    /// </summary>
    /// <param name="message">The message that contains the payload.</param>
    /// <param name="serializers">The serializers that can convert the payload.</param>
    /// <exception cref="MessagingException">No serializer has the content type of the message.</exception>
    public static T Deserialize<T>(this PluginMessage message, params IMessageSerializer[] serializers)
    {
        var serializer = serializers.FirstOrDefault(s => string.Equals(s.ContentType, message.ContentType, StringComparison.OrdinalIgnoreCase))
            ?? throw new MessagingException($"No serializer is available for the content type {message.ContentType}.");

        return serializer.Deserialize<T>(message.Payload);
    }
}
