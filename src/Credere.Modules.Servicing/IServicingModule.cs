using Credere.Core.Results;
using Credere.Core.Schedules;
using Credere.Modules.Servicing.Application.Commands.AccrueInterest;
using Credere.Modules.Servicing.Application.Commands.Activate;
using Credere.Modules.Servicing.Application.Commands.ApplyAging;
using Credere.Modules.Servicing.Application.Commands.Disburse;
using Credere.Modules.Servicing.Application.Commands.ExecutePayoff;
using Credere.Modules.Servicing.Application.Commands.RecordPayment;
using Credere.Modules.Servicing.Application.Commands.Recover;
using Credere.Modules.Servicing.Application.Commands.RegisterDisbursementIntent;
using Credere.Modules.Servicing.Application.Commands.Restructure;
using Credere.Modules.Servicing.Application.Commands.WriteOff;
using Credere.Modules.Servicing.Application.Queries.GetLoan;
using Credere.Modules.Servicing.Application.Queries.ListLoans;
using Credere.Modules.Servicing.Application.Queries.QuotePayoff;
using Credere.Modules.Servicing.Contracts;

namespace Credere.Modules.Servicing;

/// <summary>The public facade for the Servicing module.</summary>
public interface IServicingModule
{
    /// <summary>Registers accepted terms for disbursement.</summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The registered intent or a business error.</returns>
    Task<Result<DisbursementIntentView, DomainError>> RegisterDisbursementIntentAsync(
        RegisterDisbursementIntent command, CancellationToken cancellationToken);

    /// <summary>Confirms treasury disbursement.</summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The loan identifier or a business error.</returns>
    Task<Result<LoanId, DomainError>> DisburseAsync(Disburse command, CancellationToken cancellationToken);

    /// <summary>Activates a disbursed loan.</summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The command outcome.</returns>
    Task<Result<Unit, DomainError>> ActivateAsync(Activate command, CancellationToken cancellationToken);

    /// <summary>Posts accrual through the supplied business date.</summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The accrual result or a business error.</returns>
    Task<Result<AccrualResult, DomainError>> AccrueInterestAsync(
        AccrueInterest command, CancellationToken cancellationToken);

    /// <summary>Records and allocates a borrower payment.</summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The allocation and resulting loan state, or a business error.</returns>
    Task<Result<PaymentResult, DomainError>> RecordPaymentAsync(
        RecordPayment command, CancellationToken cancellationToken);

    /// <summary>Applies the supplied days past due.</summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The resulting aging information or a business error.</returns>
    Task<Result<AgingResult, DomainError>> ApplyAgingAsync(ApplyAging command, CancellationToken cancellationToken);

    /// <summary>Quotes the payoff total for the specified date.</summary>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The payoff quote or a business error.</returns>
    Task<Result<PayoffQuote, DomainError>> QuotePayoffAsync(QuotePayoff query, CancellationToken cancellationToken);

    /// <summary>Revalidates and executes a payoff on the specified date.</summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The payoff allocation or a business error.</returns>
    Task<Result<PayoffResult, DomainError>> ExecutePayoffAsync(
        ExecutePayoff command, CancellationToken cancellationToken);

    /// <summary>Writes off a defaulted loan.</summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The command outcome.</returns>
    Task<Result<Unit, DomainError>> WriteOffAsync(WriteOff command, CancellationToken cancellationToken);

    /// <summary>Records the restructuring of a defaulted loan.</summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The command outcome.</returns>
    Task<Result<Unit, DomainError>> RestructureAsync(Restructure command, CancellationToken cancellationToken);

    /// <summary>Records recovery of a defaulted loan.</summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The command outcome.</returns>
    Task<Result<Unit, DomainError>> RecoverAsync(Recover command, CancellationToken cancellationToken);

    /// <summary>Gets the loan with the specified identifier.</summary>
    /// <param name="id">The loan identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The loan, or null when it does not exist.</returns>
    Task<LoanView?> GetAsync(LoanId id, CancellationToken cancellationToken);

    /// <summary>Gets the loan's amortization schedule.</summary>
    /// <param name="id">The loan identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The schedule, or null when it is unavailable.</returns>
    Task<AmortizationSchedule?> GetScheduleAsync(LoanId id, CancellationToken cancellationToken);

    /// <summary>Lists loans that match the filters.</summary>
    /// <param name="filters">The filters applied to the loan list.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching loans.</returns>
    Task<IReadOnlyList<LoanView>> ListAsync(LoanListFilters filters, CancellationToken cancellationToken);

    /// <summary>Gets the disbursement intent with the specified identifier.</summary>
    /// <param name="id">The disbursement intent identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The intent, or null when it does not exist.</returns>
    Task<DisbursementIntentView?> GetDisbursementIntentAsync(
        DisbursementIntentId id, CancellationToken cancellationToken);
}
