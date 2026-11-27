using Credere.Core.Results;
using Credere.Modules.Servicing.Contracts;

namespace Credere.Modules.Servicing.Domain;

/// <summary>The accepted terms awaiting treasury's confirmation of disbursement.</summary>
internal sealed class DisbursementIntent
{
    internal DisbursementIntent(
        DisbursementIntentId id, Guid sourceReference, int acceptedTermsVersion, string product, AcceptedLoanTerms terms)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(acceptedTermsVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(product);
        ArgumentNullException.ThrowIfNull(terms);

        Id = id;
        SourceReference = sourceReference;
        AcceptedTermsVersion = acceptedTermsVersion;
        Product = product;
        Terms = terms;
    }

    internal DisbursementIntentId Id { get; }

    internal Guid SourceReference { get; }

    internal int AcceptedTermsVersion { get; }

    internal string Product { get; }

    internal AcceptedLoanTerms Terms { get; }

    internal DisbursementIntentState State { get; private set; } = DisbursementIntentState.Pending;

    internal LoanId? LoanId { get; private set; }

    internal Result<Unit, DomainError> MarkDisbursed(LoanId loanId)
    {
        if (State != DisbursementIntentState.Pending)
        {
            return Result<Unit, DomainError>.Failure(
                new InvalidStateTransition(State.ToString(), "Disburse", "the intent already has a loan"));
        }

        LoanId = loanId;
        State = DisbursementIntentState.Disbursed;
        return Result<Unit, DomainError>.Success(Unit.Value);
    }
}
