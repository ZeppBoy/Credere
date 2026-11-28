using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace Credere.Modules.Origination.Infrastructure;

/// <summary>Configures and registers the module's persistence, so the host, the tests and <c>dotnet ef</c> agree.</summary>
internal static class OriginationPersistence
{
    /// <summary>Points a context at SQL Server with the module's own migrations history table (ADR-0003).</summary>
    /// <param name="builder">The options builder.</param>
    /// <param name="connectionString">The SQL Server connection string.</param>
    internal static void Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        builder.UseSqlServer(
            connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", OriginationSchema.Name));
    }

    /// <summary>
    /// Registers the context, repository and reader. Each is scoped, so one command or query owns one unit of work.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <returns>The same collection.</returns>
    /// <remarks>The host registers an <see cref="IClock"/>. <c>SystemClock.Instance</c> appears only in the host.</remarks>
    internal static IServiceCollection AddOriginationPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<OriginationDbContext>(builder => Configure(builder, connectionString));
        services.AddScoped<LoanApplicationRepository>();
        services.AddScoped<ApplicationReader>();
        return services;
    }
}
