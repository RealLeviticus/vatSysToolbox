using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace vatSysToolbox.Infrastructure;

public static class InfrastructureServiceCollectionExtensionMethods
{
    /// <summary>
    /// Registers <see cref="IClock"/>.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    public static IServiceCollection AddClock(this IServiceCollection services)
    {
        services.TryAddSingleton<IClock, SystemClock>();
        return services;
    }
}
