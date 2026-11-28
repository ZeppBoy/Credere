using Credere.Modules.Servicing.Infrastructure;
using Credere.Modules.Servicing.Infrastructure.Converters;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Credere.Modules.Servicing.Tests.Infrastructure;

/// <summary>
/// Checks the shape of the EF model without a database: building a model opens no connection. The rows and the
/// migration are checked against real SQL Server in <see cref="ServicingMigrationTests"/>.
/// </summary>
public sealed class ServicingModelTests
{
    private static IModel BuildModel()
    {
        using var context = CreateContext();
        return context.GetService<IDesignTimeModel>().Model;
    }

    private static ServicingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ServicingDbContext>();
        ServicingPersistence.Configure(options, "Server=localhost,1433;Database=ModelOnly;TrustServerCertificate=true");
        return new ServicingDbContext(options.Options);
    }

    [Fact]
    public void Model_EveryTableLivesInTheServicingSchema()
    {
        var tables = BuildModel().GetEntityTypes().Select(entity => (entity.GetTableName(), entity.GetSchema())).ToList();

        tables.Should().NotBeEmpty().And.OnlyContain(table => table.Item2 == "servicing");
    }

    [Fact]
    public void Model_HasTheDocumentedTables()
    {
        var names = BuildModel().GetEntityTypes().Select(entity => entity.GetTableName()).ToList();

        names.Should().BeEquivalentTo(
            "Loans",
            "LoanEvents",
            "DisbursementIntents",
            "Outbox",
            "LoanSchedules",
            "AccrualLedger",
            "PaymentLedger",
            "CommandReceipts_RegisterDisbursementIntent",
            "CommandReceipts_Disburse",
            "CommandReceipts_Activate",
            "CommandReceipts_AccrueInterest",
            "CommandReceipts_RecordPayment",
            "CommandReceipts_ApplyAging",
            "CommandReceipts_ExecutePayoff",
            "CommandReceipts_WriteOff",
            "CommandReceipts_Restructure",
            "CommandReceipts_Recover");
    }

    [Fact]
    public void ReceiptHandlers_NamesMatchTheCommandsOfTheFacade()
    {
        var commands = typeof(IServicingModule).GetMethods()
            .Select(method => method.Name)
            .Where(name => name.EndsWith("Async", StringComparison.Ordinal))
            .Select(name => name[..^"Async".Length])
            .ToList();

        // Queries return values, not Results of commands. The ten commands are the ones that keep a receipt.
        commands.Should().Contain(ServicingSchema.ReceiptHandlers);
    }

    [Fact]
    public void Model_EveryDecimalIsStoredAsExactText_BecauseNoDecimalColumnHoldsEveryAcceptedValue()
    {
        var decimals = BuildModel().GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Where(property => (Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) == typeof(decimal))
            .ToList();

        decimals.Should().NotBeEmpty()
            .And.OnlyContain(property => property.GetValueConverter() is DecimalTextConverter && property.GetMaxLength() == 32);
    }

    [Fact]
    public void Model_StateRowsCarryARowVersion()
    {
        var model = BuildModel();

        foreach (var type in new[] { typeof(LoanRow), typeof(DisbursementIntentRow) })
        {
            var property = model.FindEntityType(type)!.FindProperty("RowVersion")!;
            property.IsConcurrencyToken.Should().BeTrue();
            property.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
        }
    }

    [Fact]
    public void Model_HistoryHasAUniqueIndexOnTheStreamAndItsVersion()
    {
        var entity = BuildModel().FindEntityType(typeof(LoanEventRow))!;

        var index = entity.GetIndexes().Single(candidate => candidate.IsUnique);

        index.Properties.Select(property => property.Name).Should().Equal("StreamId", "StreamVersion");
    }

    [Fact]
    public void Model_TheIntentKeyAndTheLoanOfAnIntentAreUnique()
    {
        var model = BuildModel();

        var intentKey = model.FindEntityType(typeof(DisbursementIntentRow))!.GetIndexes()
            .Single(index => index.Properties.Count == 2);
        var loanOfIntent = model.FindEntityType(typeof(LoanRow))!.GetIndexes()
            .Single(index => index.Properties.Count == 1 && index.Properties[0].Name == "DisbursementIntentId");

        intentKey.IsUnique.Should().BeTrue();
        intentKey.Properties.Select(property => property.Name).Should().Equal("SourceReference", "AcceptedTermsVersion");
        loanOfIntent.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void Model_TheAccrualLedgerIsKeyedByTheLoanAndTheBusinessDate()
    {
        var key = BuildModel().FindEntityType(typeof(AccrualLedgerRow))!.FindPrimaryKey()!;

        key.Properties.Select(property => property.Name).Should().Equal("LoanId", "BusinessDate");
    }

    [Fact]
    public void Model_ThePaymentLedgerIsKeyedByThePaymentId()
    {
        var key = BuildModel().FindEntityType(typeof(PaymentLedgerRow))!.FindPrimaryKey()!;

        key.Properties.Select(property => property.Name).Should().Equal("PaymentId");
    }

    [Fact]
    public void Model_EveryReceiptTableIsKeyedByTheCommandId()
    {
        var receipts = BuildModel().GetEntityTypes().Where(entity => entity.ClrType == typeof(CommandReceipt)).ToList();

        receipts.Should().HaveCount(10);
        receipts.Should().OnlyContain(entity => entity.FindPrimaryKey()!.Properties.Single().Name == "CommandId");
    }

    [Fact]
    public void Receipts_HandlerWithoutATable_Throws()
    {
        using var context = CreateContext();

        var act = () => context.Receipts("NoSuchHandler");

        act.Should().Throw<ArgumentException>();
    }
}
