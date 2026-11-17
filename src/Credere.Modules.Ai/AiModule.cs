using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Credere.Modules.Ai;

/// <summary>Public contract for the Ai module.</summary>
/// <remarks>M5 implementation is pending.</remarks>
public interface IAiModule
{
}

/// <summary>DI extension methods for the Ai module.</summary>
public static class AiModuleExtensions
{
    /// <summary>Registers the Ai module's services. No-op until M5.</summary>
    public static IServiceCollection AddAiModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }
}
