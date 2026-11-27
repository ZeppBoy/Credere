using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Credere.Modules.Origination.Tests.Infrastructure;

[Collection(SqlServerTestGroup.Name)]
[Trait("Requires", "Docker")]
public sealed class OriginationMigrationTests(SqlServerFixture fixture)
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
    public async Task Migrations_CreateEveryDocumentedTableInTheOriginationSchema()
    {
        var tables = await QueryAsync<string>(
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'origination'");

        tables.Should().BeEquivalentTo(
            "__EFMigrationsHistory",
            "LoanApplications",
            "LoanApplicationEvents",
            "Outbox",
            "CommandReceipts_SubmitApplication",
            "CommandReceipts_DecideApplication",
            "CommandReceipts_AcceptTerms",
            "CommandReceipts_WithdrawApplication");
    }

    [Fact]
    public async Task Migrations_KeepTheirHistoryInTheOriginationSchemaOnly()
    {
        var elsewhere = await QueryAsync<string>(
            "SELECT TABLE_SCHEMA FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__EFMigrationsHistory' AND TABLE_SCHEMA <> 'origination'");

        elsewhere.Should().BeEmpty();
    }

    [Theory]
    [InlineData("MonthlyIncomeAmount")]
    [InlineData("MonthlyDebtPaymentsAmount")]
    [InlineData("RequestedPrincipalAmount")]
    [InlineData("DecidedPrincipalAmount")]
    [InlineData("DecidedNominalRate")]
    public async Task Migrations_StoreEveryDecimalAsVarchar32ExactText(string column)
    {
        var found = await QueryAsync<(string, int)>(
            $"SELECT DATA_TYPE, CAST(CHARACTER_MAXIMUM_LENGTH AS int) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'origination' AND TABLE_NAME = 'LoanApplications' AND COLUMN_NAME = '{column}'");

        found.Should().ContainSingle().Which.Should().Be(("varchar", 32));
    }

    [Fact]
    public async Task Migrations_LeaveNoDecimalColumnInTheSchema()
    {
        var found = await QueryAsync<string>(
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'origination' AND DATA_TYPE IN ('decimal', 'numeric', 'money', 'smallmoney', 'float', 'real')");

        found.Should().BeEmpty();
    }

    [Theory]
    [InlineData("LoanApplications", "SubmittedAt")]
    [InlineData("LoanApplicationEvents", "OccurredAt")]
    [InlineData("LoanApplicationEvents", "RecordedAt")]
    [InlineData("Outbox", "DispatchedAt")]
    [InlineData("CommandReceipts_SubmitApplication", "ReceivedAt")]
    public async Task Migrations_StoreInstantsAsDatetime2WithSevenFractionalDigits(string table, string column)
    {
        var found = await QueryAsync<(string, int)>(
            $"SELECT DATA_TYPE, CAST(DATETIME_PRECISION AS int) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'origination' AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}'");

        found.Should().ContainSingle().Which.Should().Be(("datetime2", 7));
    }

    [Fact]
    public async Task Migrations_StoreDateOfBirthAsADate()
    {
        var found = await QueryAsync<string>(
            "SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'origination' AND TABLE_NAME = 'LoanApplications' AND COLUMN_NAME = 'DateOfBirth'");

        found.Should().ContainSingle().Which.Should().Be("date");
    }

    [Fact]
    public async Task Migrations_StateRowHasARowVersionColumn()
    {
        var found = await QueryAsync<string>(
            "SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'origination' AND TABLE_NAME = 'LoanApplications' AND COLUMN_NAME = 'RowVersion'");

        found.Should().ContainSingle().Which.Should().Be("timestamp");
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

        if (typeof(T) == typeof((int, int)))
        {
            return (T)(object)(reader.GetInt32(0), reader.GetInt32(1));
        }

        if (typeof(T) == typeof((string, int)))
        {
            return (T)(object)(reader.GetString(0), reader.GetInt32(1));
        }

        throw new NotSupportedException(typeof(T).Name);
    }
}
