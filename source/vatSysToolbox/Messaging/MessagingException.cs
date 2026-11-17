namespace vatSysToolbox.Messaging;

/// <summary>
/// The exception that occurs when a message cannot be sent, received, or handled.
/// </summary>
/// <param name="message">The message that describes the error.</param>
public class MessagingException(string message) : Exception(message);
