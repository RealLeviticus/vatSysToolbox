using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using vatSysToolbox.Infrastructure;

namespace vatSysToolbox.BackgroundWork;

public static class BackgroundWorkServiceCollectionExtensionMethods
{
    /// <summary>
    /// Registers <see cref="WorkQueue"/> and <see cref="IClock"/>.
    /// The queue uses the registered <see cref="IErrorReporter"/>, or <see cref="NullErrorReporter"/> if no error
    /// reporter is registered.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    public static IServiceCollection AddWorkQueue(this IServiceCollection services)
    {
        services.AddClock();
        services.TryAddSingleton(serviceProvider => new WorkQueue(
            serviceProvider.GetErrorReporterOrDefault(),
            serviceProvider.GetRequiredService<ILogger<WorkQueue>>(),
            serviceProvider.GetRequiredService<IClock>()));

        return services;
    }
}
