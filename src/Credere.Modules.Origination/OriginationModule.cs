using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Credere.Modules.Origination;

/// <summary>
/// Public contract for the Origination module.
/// </summary>
/// <remarks>
/// M2 implementation is pending. The interface is declared here so hosts can
/// reference the module at composition time.
/// </remarks>
public interface IOriginationModule
{
}

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
