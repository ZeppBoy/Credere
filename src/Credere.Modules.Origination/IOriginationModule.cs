using Credere.Core.Results;
using Credere.Modules.Origination.Application.Commands.AcceptTerms;
using Credere.Modules.Origination.Application.Commands.DecideApplication;
using Credere.Modules.Origination.Application.Commands.SubmitApplication;
using Credere.Modules.Origination.Application.Commands.WithdrawApplication;
using Credere.Modules.Origination.Application.Queries.GetApplication;
using Credere.Modules.Origination.Application.Queries.ListApplications;
using Credere.Modules.Origination.Contracts;

namespace Credere.Modules.Origination;

/// <summary>The public facade for the Origination module.</summary>
public interface IOriginationModule
{
    /// <summary>Submits a loan application.</summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The new application identifier or a business error.</returns>
    Task<Result<LoanApplicationId, DomainError>> SubmitAsync(
        SubmitApplication command, CancellationToken cancellationToken);

    /// <summary>Records an underwriting outcome.</summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The recorded decision and resulting application state, or a business error.</returns>
    Task<Result<DecisionView, DomainError>> DecideAsync(
        DecideApplication command, CancellationToken cancellationToken);

    /// <summary>Records the borrower's acceptance of the specified terms version.</summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The command outcome.</returns>
    Task<Result<Unit, DomainError>> AcceptTermsAsync(AcceptTerms command, CancellationToken cancellationToken);

    /// <summary>Withdraws a loan application.</summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The command outcome.</returns>
    Task<Result<Unit, DomainError>> WithdrawAsync(WithdrawApplication command, CancellationToken cancellationToken);

    /// <summary>Gets the application with the specified identifier.</summary>
    /// <param name="id">The application identifier.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The application, or null when it does not exist.</returns>
    Task<ApplicationView?> GetAsync(LoanApplicationId id, CancellationToken cancellationToken);

    /// <summary>Lists applications that match the filters.</summary>
    /// <param name="filters">The filters applied to the application list.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The matching applications.</returns>
    Task<IReadOnlyList<ApplicationView>> ListAsync(
        ApplicationListFilters filters, CancellationToken cancellationToken);
}
