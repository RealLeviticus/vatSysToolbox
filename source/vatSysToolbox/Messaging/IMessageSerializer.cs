using System.Text.Json;

namespace vatSysToolbox.Messaging;

/// <summary>
/// Converts between message data and bytes.
/// </summary>
public interface IMessageSerializer
{
    /// <summary>
    /// The format (for example, <c>application/json</c>).
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Converts the value to bytes.
    /// </summary>
    /// <param name="value">The value.</param>
    byte[] Serialize<T>(T value);

    /// <summary>
    /// Converts the bytes to a value.
    /// </summary>
    /// <param name="payload">The bytes.</param>
    T Deserialize<T>(byte[] payload);
}

/// <summary>
/// Converts message data to JSON, and JSON to message data.
/// </summary>
/// <param name="options">The JSON options. If <c>null</c>, the serializer uses the default options.</param>
public class JsonMessageSerializer(JsonSerializerOptions? options = null) : IMessageSerializer
{
    /// <inheritdoc/>
    public string ContentType => "application/json";

    /// <inheritdoc/>
    public byte[] Serialize<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, options);
    }

    /// <inheritdoc/>
    public T Deserialize<T>(byte[] payload)
    {
        return JsonSerializer.Deserialize<T>(payload, options)
            ?? throw new MessagingException($"The JSON payload for {typeof(T)} is null.");
    }
}
