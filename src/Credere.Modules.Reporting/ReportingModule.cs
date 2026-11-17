using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Credere.Modules.Reporting;

/// <summary>Public contract for the Reporting module.</summary>
/// <remarks>M4 implementation is pending.</remarks>
public interface IReportingModule
{
}

/// <summary>DI extension methods for the Reporting module.</summary>
public static class ReportingModuleExtensions
{
    /// <summary>Registers the Reporting module's services. No-op until M4.</summary>
    public static IServiceCollection AddReportingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }
}
