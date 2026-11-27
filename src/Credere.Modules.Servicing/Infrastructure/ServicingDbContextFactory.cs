using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Credere.Modules.Servicing.Infrastructure;

/// <summary>
/// Lets <c>dotnet ef</c> build a <see cref="ServicingDbContext"/> at design time, to add and script migrations. It
/// never opens a connection for <c>migrations add</c>.
/// </summary>
/// <remarks>
/// Set <c>CREDERE_SERVICING_CONNECTION</c> to point <c>database update</c> at a real server. The default holds no
/// credentials, so no secret reaches source control (AGENTS.md section 10).
/// </remarks>
internal sealed class ServicingDbContextFactory : IDesignTimeDbContextFactory<ServicingDbContext>
{
    /// <summary>The environment variable that overrides the design-time connection string.</summary>
    internal const string ConnectionStringVariable = "CREDERE_SERVICING_CONNECTION";

    private const string DefaultConnectionString =
        "Server=localhost,1433;Database=Credere;Integrated Security=false;TrustServerCertificate=true";

    /// <inheritdoc />
    public ServicingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        var builder = new DbContextOptionsBuilder<ServicingDbContext>();
        ServicingPersistence.Configure(
            builder,
            string.IsNullOrWhiteSpace(connectionString) ? DefaultConnectionString : connectionString);
        return new ServicingDbContext(builder.Options);
    }
}
