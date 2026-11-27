using Xunit;

namespace Credere.Modules.Origination.Tests.Infrastructure;

/// <summary>Shares one <see cref="SqlServerFixture"/> between every test class marked with this collection.</summary>
[CollectionDefinition(Name)]
public sealed class SqlServerTestGroup : ICollectionFixture<SqlServerFixture>
{
    /// <summary>The collection name.</summary>
    public const string Name = "SQL Server";
}
