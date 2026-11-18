using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace vatSysToolbox.Windows;

public static class WindowsServiceCollectionExtensionMethods
{
    /// <summary>
    /// Registers <see cref="WindowManager"/> and <see cref="IGuiInvoker"/>.
    /// They use the registered <see cref="IErrorReporter"/>, or <see cref="NullErrorReporter"/> if no error reporter
    /// is registered.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    public static IServiceCollection AddWindowManager(this IServiceCollection services)
    {
        services.TryAddSingleton<IGuiInvoker>(serviceProvider => new GuiInvoker(
            serviceProvider.GetErrorReporterOrDefault()));

        services.TryAddSingleton(serviceProvider => new WindowManager(
            serviceProvider.GetRequiredService<IGuiInvoker>(),
            serviceProvider.GetErrorReporterOrDefault()));

        return services;
    }
}
