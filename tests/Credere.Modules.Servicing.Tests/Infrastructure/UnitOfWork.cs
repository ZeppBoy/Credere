using Credere.Modules.Servicing.Infrastructure;

namespace Credere.Modules.Servicing.Tests.Infrastructure;

/// <summary>
/// One command's worth of persistence: a new context with its repository and reader, as the host's scope would give it.
/// A retry after a conflict uses a new one, because the failed context holds stale rows.
/// </summary>
internal sealed class UnitOfWork : IAsyncDisposable
{
    internal UnitOfWork(SqlServerFixture fixture, FakeClock clock)
    {
        Db = fixture.CreateContext();
        Repository = new ServicingRepository(Db, clock);
        Reader = new ServicingReader(Db);
    }

    internal ServicingDbContext Db { get; }

    internal ServicingRepository Repository { get; }

    internal ServicingReader Reader { get; }

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}
