using Credere.Modules.Servicing.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace Credere.Modules.Servicing.Tests.Infrastructure;

/// <summary>
/// One real SQL Server 2022 container for every persistence test, with the module's migrations applied once. The
/// image is x64 only and runs emulated on Apple Silicon, so starting one per test would make the suite very slow.
/// Each test owns its data by creating rows under ids and product keys of its own, and never deletes rows to reset.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string DatabaseName = "Credere_Servicing_Tests";

    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    /// <summary>Gets the connection string of the migrated test database.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);

        var master = new SqlConnectionStringBuilder(_container.GetConnectionString());
        var connectionString = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = DatabaseName };
        ConnectionString = connectionString.ConnectionString;

        await using (var connection = new SqlConnection(master.ConnectionString))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{DatabaseName}]";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using var context = CreateContext();
        await context.Database.MigrateAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);

    /// <summary>Creates a context over the migrated database. The caller disposes it.</summary>
    /// <returns>A new context.</returns>
    internal ServicingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ServicingDbContext>();
        ServicingPersistence.Configure(options, ConnectionString);
        return new ServicingDbContext(options.Options);
    }
}
