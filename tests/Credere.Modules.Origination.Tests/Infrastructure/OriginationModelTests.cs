using Credere.Modules.Origination.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Credere.Modules.Origination.Tests.Infrastructure;

/// <summary>
/// Checks the shape of the EF model without a database: building a model opens no connection. The rows and the
/// migration are checked against real SQL Server in <see cref="OriginationMigrationTests"/>.
/// </summary>
public sealed class OriginationModelTests
{
    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<OriginationDbContext>();
        OriginationPersistence.Configure(options, "Server=localhost,1433;Database=ModelOnly;TrustServerCertificate=true");
        using var context = new OriginationDbContext(options.Options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void Model_EveryTableLivesInTheOriginationSchema()
    {
        var tables = BuildModel().GetEntityTypes().Select(entity => (entity.GetTableName(), entity.GetSchema())).ToList();

        tables.Should().NotBeEmpty().And.OnlyContain(table => table.Item2 == "origination");
    }

    [Fact]
    public void Model_HasTheDocumentedTables()
    {
        var names = BuildModel().GetEntityTypes().Select(entity => entity.GetTableName()).ToList();

        names.Should().BeEquivalentTo(
            "LoanApplications",
            "LoanApplicationEvents",
            "Outbox",
            "CommandReceipts_SubmitApplication",
            "CommandReceipts_DecideApplication",
            "CommandReceipts_AcceptTerms",
            "CommandReceipts_WithdrawApplication");
    }

    [Fact]
    public void Model_EveryDecimalColumnHasAnExplicitPrecision()
    {
        var decimals = BuildModel().GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Where(property => (Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) == typeof(decimal))
            .ToList();

        decimals.Should().NotBeEmpty().And.OnlyContain(property => property.GetPrecision() != null && property.GetScale() != null);
    }

    [Fact]
    public void Model_StateRowCarriesARowVersion()
    {
        var entity = BuildModel().FindEntityType(typeof(LoanApplicationRow))!;

        var property = entity.FindProperty(nameof(LoanApplicationRow.RowVersion))!;

        property.IsConcurrencyToken.Should().BeTrue();
        property.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
    }

    [Fact]
    public void Model_HistoryHasAUniqueIndexOnTheStreamAndItsVersion()
    {
        var entity = BuildModel().FindEntityType(typeof(LoanApplicationEventRow))!;

        var index = entity.GetIndexes().Single(candidate => candidate.IsUnique);

        index.Properties.Select(property => property.Name)
            .Should().Equal(nameof(LoanApplicationEventRow.StreamId), nameof(LoanApplicationEventRow.StreamVersion));
    }

    [Fact]
    public void Model_EveryReceiptTableIsKeyedByTheCommandId()
    {
        var receipts = BuildModel().GetEntityTypes().Where(entity => entity.ClrType == typeof(CommandReceipt)).ToList();

        receipts.Should().HaveCount(4);
        receipts.Should().OnlyContain(entity => entity.FindPrimaryKey()!.Properties.Single().Name == nameof(CommandReceipt.CommandId));
    }

    [Fact]
    public void Receipts_HandlerWithoutATable_Throws()
    {
        var options = new DbContextOptionsBuilder<OriginationDbContext>();
        OriginationPersistence.Configure(options, "Server=localhost,1433;Database=ModelOnly;TrustServerCertificate=true");
        using var context = new OriginationDbContext(options.Options);

        var act = () => context.Receipts("NoSuchHandler");

        act.Should().Throw<ArgumentException>();
    }
}
