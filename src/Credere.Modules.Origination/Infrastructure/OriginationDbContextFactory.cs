using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Credere.Modules.Origination.Infrastructure;

/// <summary>
/// Lets <c>dotnet ef</c> build an <see cref="OriginationDbContext"/> at design time, to add and script migrations.
/// It never opens a connection for <c>migrations add</c>.
/// </summary>
/// <remarks>
/// Set <c>CREDERE_ORIGINATION_CONNECTION</c> to point <c>database update</c> at a real server. The default holds no
/// credentials, so no secret reaches source control (AGENTS.md section 10).
/// </remarks>
internal sealed class OriginationDbContextFactory : IDesignTimeDbContextFactory<OriginationDbContext>
{
    /// <summary>The environment variable that overrides the design-time connection string.</summary>
    internal const string ConnectionStringVariable = "CREDERE_ORIGINATION_CONNECTION";

    private const string DefaultConnectionString =
        "Server=localhost,1433;Database=Credere;Integrated Security=false;TrustServerCertificate=true";

    /// <inheritdoc />
    public OriginationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        var builder = new DbContextOptionsBuilder<OriginationDbContext>();
        OriginationPersistence.Configure(
            builder,
            string.IsNullOrWhiteSpace(connectionString) ? DefaultConnectionString : connectionString);
        return new OriginationDbContext(builder.Options);
    }
}
