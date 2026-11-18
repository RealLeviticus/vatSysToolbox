using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using vatSysToolbox.Infrastructure;

namespace vatSysToolbox;

public static class ServiceCollectionExtensionMethods
{
    /// <summary>
    /// Registers <see cref="IErrorReporter"/> and <see cref="IClock"/>.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <param name="pluginName">The name that shows with the errors in the vatSys error window.</param>
    public static IServiceCollection AddErrorReporter(this IServiceCollection services, string pluginName)
    {
        services.AddClock();
        services.TryAddSingleton<IErrorReporter>(serviceProvider => new ErrorReporter(
            pluginName,
            serviceProvider.GetRequiredService<ILogger<ErrorReporter>>(),
            serviceProvider.GetRequiredService<IClock>()));

        return services;
    }

    /// <summary>
    /// Gets the registered <see cref="IErrorReporter"/>, or <see cref="NullErrorReporter"/> if no error reporter is
    /// registered.
    /// </summary>
    /// <param name="serviceProvider">The service provider that has the error reporter.</param>
    internal static IErrorReporter GetErrorReporterOrDefault(this IServiceProvider serviceProvider)
    {
        return serviceProvider.GetService<IErrorReporter>() ?? NullErrorReporter.Instance;
    }
}
