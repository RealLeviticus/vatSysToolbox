using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace vatSysToolbox.Messaging;

public static class MessagingServiceCollectionExtensionMethods
{
    /// <summary>
    /// Registers <see cref="IPluginMessenger"/> and <see cref="IMessageTransport"/>.
    /// The messenger uses the registered <see cref="IErrorReporter"/>, or <see cref="NullErrorReporter"/> if no error
    /// reporter is registered.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <param name="mailboxName">
    /// The name that other plugins use to send messages to the plugin. Use a reverse DNS name (for example,
    /// <c>dev.example.my-plugin</c>).
    /// </param>
    public static IServiceCollection AddPluginMessenger(this IServiceCollection services, string mailboxName)
    {
        services.TryAddSingleton<IMessageTransport, InProcessTransport>();
        services.TryAddSingleton<IPluginMessenger>(serviceProvider => new PluginMessenger(
            mailboxName,
            serviceProvider.GetRequiredService<IMessageTransport>(),
            serviceProvider.GetErrorReporterOrDefault(),
            serviceProvider.GetRequiredService<ILogger<PluginMessenger>>()));

        return services;
    }
}
