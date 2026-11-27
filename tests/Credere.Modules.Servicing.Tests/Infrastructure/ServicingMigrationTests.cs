using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Credere.Modules.Servicing.Tests.Infrastructure;

[Collection(SqlServerTestGroup.Name)]
[Trait("Requires", "Docker")]
public sealed class ServicingMigrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Migrations_AppliedToAnEmptyDatabase_MatchTheModel()
    {
        await using var context = fixture.CreateContext();

        var pending = await context.Database.GetPendingMigrationsAsync();

        pending.Should().BeEmpty("the fixture applied every migration");
        context.Database.HasPendingModelChanges().Should().BeFalse("the migration must describe the current model");
    }

    [Fact]
    public async Task Migrations_CreateEveryDocumentedTableInTheServicingSchema()
    {
        var tables = await QueryAsync<string>(
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'servicing'");

        tables.Should().BeEquivalentTo(
            "__EFMigrationsHistory",
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
    public async Task Migrations_KeepTheirHistoryInTheServicingSchemaOnly()
    {
        var elsewhere = await QueryAsync<string>(
            "SELECT TABLE_SCHEMA FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__EFMigrationsHistory' AND TABLE_SCHEMA <> 'servicing'");

        elsewhere.Should().BeEmpty();
    }

    [Fact]
    public async Task Migrations_LeaveNoDecimalColumnInTheSchema()
    {
        var found = await QueryAsync<string>(
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'servicing' AND DATA_TYPE IN ('decimal', 'numeric', 'money', 'smallmoney', 'float', 'real')");

        found.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Loans", "RemainingPrincipal")]
    [InlineData("Loans", "NominalRate")]
    [InlineData("DisbursementIntents", "Principal")]
    [InlineData("AccrualLedger", "Amount")]
    [InlineData("PaymentLedger", "Excess")]
    [InlineData("LoanSchedules", "InterestDue")]
    public async Task Migrations_StoreEveryDecimalAsVarchar32ExactText(string table, string column)
    {
        var found = await QueryAsync<(string, int)>(
            $"SELECT DATA_TYPE, CAST(CHARACTER_MAXIMUM_LENGTH AS int) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'servicing' AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}'");

        found.Should().ContainSingle().Which.Should().Be(("varchar", 32));
    }

    [Theory]
    [InlineData("LoanEvents", "RecordedAt", "datetime2")]
    [InlineData("DisbursementIntents", "RegisteredAt", "datetime2")]
    [InlineData("Outbox", "DispatchedAt", "datetime2")]
    [InlineData("CommandReceipts_Activate", "ReceivedAt", "datetime2")]
    [InlineData("LoanEvents", "OccurredAt", "date")]
    [InlineData("Loans", "AccruedThroughDate", "date")]
    [InlineData("AccrualLedger", "BusinessDate", "date")]
    [InlineData("PaymentLedger", "EffectiveDate", "date")]
    [InlineData("LoanSchedules", "PaymentDate", "date")]
    public async Task Migrations_StoreInstantsAsDatetime2AndBusinessDatesAsDate(string table, string column, string type)
    {
        var found = await QueryAsync<(string, int)>(
            $"SELECT DATA_TYPE, CAST(ISNULL(DATETIME_PRECISION, 0) AS int) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'servicing' AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}'");

        found.Should().ContainSingle().Which.Item1.Should().Be(type);
        if (type == "datetime2")
        {
            found[0].Item2.Should().Be(7);
        }
    }

    [Theory]
    [InlineData("Loans")]
    [InlineData("DisbursementIntents")]
    public async Task Migrations_StateRowsHaveARowVersionColumn(string table)
    {
        var found = await QueryAsync<string>(
            $"SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'servicing' AND TABLE_NAME = '{table}' AND COLUMN_NAME = 'RowVersion'");

        found.Should().ContainSingle().Which.Should().Be("timestamp");
    }

    [Theory]
    [InlineData("DisbursementIntents", "IX_DisbursementIntents_SourceReference_AcceptedTermsVersion")]
    [InlineData("Loans", "IX_Loans_DisbursementIntentId")]
    [InlineData("LoanEvents", "IX_LoanEvents_StreamId_StreamVersion")]
    public async Task Migrations_CreateTheUniqueIndexesTheModuleDocNames(string table, string index)
    {
        var found = await QueryAsync<string>(
            $"SELECT i.name FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE s.name = 'servicing' AND t.name = '{table}' AND i.name = '{index}' AND i.is_unique = 1");

        found.Should().ContainSingle();
    }

    private async Task<List<T>> QueryAsync<T>(string sql)
    {
        var rows = new List<T>();
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(Read<T>(reader));
        }

        return rows;
    }

    private static T Read<T>(SqlDataReader reader)
    {
        if (typeof(T) == typeof(string))
        {
            return (T)(object)reader.GetString(0);
        }

        if (typeof(T) == typeof((string, int)))
        {
            return (T)(object)(reader.GetString(0), reader.GetInt32(1));
        }

        throw new NotSupportedException(typeof(T).Name);
    }
}
