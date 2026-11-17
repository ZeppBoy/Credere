using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Credere.Modules.Documents;

/// <summary>
/// Public contract for the Documents module.
/// </summary>
/// <remarks>M3 implementation is pending.</remarks>
public interface IDocumentsModule
{
}

/// <summary>DI extension methods for the Documents module.</summary>
public static class DocumentsModuleExtensions
{
    /// <summary>Registers the Documents module's services. No-op until M3.</summary>
    public static IServiceCollection AddDocumentsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }
}
