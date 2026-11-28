using Credere.Modules.Origination.Infrastructure;

namespace Credere.Modules.Origination.Tests.Infrastructure;

/// <summary>
/// One command's worth of persistence: a new context with its repository and reader, as the host's scope would give it.
/// A retry after a conflict uses a new one, because the failed context holds stale rows.
/// </summary>
internal sealed class UnitOfWork : IAsyncDisposable
{
    internal UnitOfWork(SqlServerFixture fixture, FakeClock clock)
    {
        Db = fixture.CreateContext();
        Repository = new LoanApplicationRepository(Db, clock);
        Reader = new ApplicationReader(Db);
    }

    internal OriginationDbContext Db { get; }

    internal LoanApplicationRepository Repository { get; }

    internal ApplicationReader Reader { get; }

    public ValueTask DisposeAsync() => Db.DisposeAsync();
}
