using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Credere.Modules.Origination;

/// <summary>
/// DI extension methods for the Origination module.
/// </summary>
public static class OriginationModuleExtensions
{
    /// <summary>Registers the Origination module's services. No-op until M2.</summary>
    public static IServiceCollection AddOriginationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }
}
