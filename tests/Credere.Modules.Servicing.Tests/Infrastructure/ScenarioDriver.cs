using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Domain;
using FluentAssertions;
using NodaTime;
using static Credere.Modules.Servicing.Tests.Infrastructure.PersistenceSamples;

namespace Credere.Modules.Servicing.Tests.Infrastructure;

/// <summary>
/// Drives the flows a handler will run against the repository, one unit of work for each step as the host's scope
/// would give them, so a test can set up a stored intent or loan in one call.
/// </summary>
internal sealed class ScenarioDriver(SqlServerFixture fixture, FakeClock clock)
{
    internal UnitOfWork Unit() => new(fixture, clock);

    internal async Task<DisbursementIntent> RegisterIntentAsync(DisbursementIntent? intent = null)
    {
        var stored = intent ?? NewIntent();
        await using var unit = Unit();
        unit.Repository.AddIntent(stored);
        var result = await unit.Repository.CommitAsync("DisbursementIntent", stored.Id.Value, CancellationToken.None);
        result.IsSuccess.Should().BeTrue("the test setup must store the intent");
        return stored;
    }

    /// <summary>Registers an intent, then disburses it into a stored loan, as <c>Disburse</c> does.</summary>
    internal async Task<(LoanId LoanId, DisbursementIntentId IntentId, string Product)> DisburseAsync(
        string? product = null,
        LocalDate? disbursedOn = null)
    {
        var intent = await RegisterIntentAsync(NewIntent(product: product));
        var loanId = NewLoanId();
        await using var unit = Unit();
        var loaded = (await unit.Repository.FindIntentAsync(intent.Id, CancellationToken.None))!;
        var loan = NewLoan(loanId, loaded, disbursedOn);
        Succeeded(loaded.MarkDisbursed(loanId));
        unit.Repository.AddLoan(loan);
        unit.Repository.UpdateIntent(loaded);
        var result = await unit.Repository.CommitAsync("Loan", loanId.Value, CancellationToken.None);
        result.IsSuccess.Should().BeTrue("the test setup must store the loan");
        return (loanId, intent.Id, intent.Product);
    }

    /// <summary>Loads a loan, applies a command to it and stores the result, as a command handler does.</summary>
    internal async Task ChangeLoanAsync(LoanId id, Action<Loan> change)
    {
        await using var unit = Unit();
        var loan = (await unit.Repository.FindLoanAsync(id, CancellationToken.None))!;
        change(loan);
        unit.Repository.UpdateLoan(loan);
        var result = await unit.Repository.CommitAsync("Loan", id.Value, CancellationToken.None);
        result.IsSuccess.Should().BeTrue("the test setup must store the change");
    }
}
