using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Credere.Modules.Servicing;

/// <summary>DI extension methods for the Servicing module.</summary>
public static class ServicingModuleExtensions
{
    /// <summary>Registers the Servicing module's services. No-op until M2.</summary>
    public static IServiceCollection AddServicingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }
}
