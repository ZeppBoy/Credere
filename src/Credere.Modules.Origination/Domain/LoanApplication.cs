using Credere.Core.Results;
using Credere.Modules.Origination.Application.Commands.AcceptTerms;
using Credere.Modules.Origination.Application.Commands.DecideApplication;
using Credere.Modules.Origination.Application.Commands.SubmitApplication;
using Credere.Modules.Origination.Application.Commands.WithdrawApplication;
using Credere.Modules.Origination.Contracts;
using Credere.Modules.Origination.Domain.Events;
using NodaTime;

namespace Credere.Modules.Origination.Domain;

/// <summary>
/// A borrower's request for a loan and the lender's response, from submission to acceptance, decline or withdrawal
/// (docs/modules/origination.md, "Aggregates"). The aggregate does no I/O and never reads the clock: the handler
/// passes the <see cref="Instant"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every command method returns <see cref="Result{T, E}"/> and appends one domain event on success; an expected
/// failure changes nothing. The permitted transitions are the table in the module doc, held in one place
/// (<see cref="IsAllowed"/>), which both the command methods and <see cref="Rehydrate"/> use.
/// </para>
/// <para>
/// A command's <c>Id</c> must be this application's id. The handler loads the aggregate by that id, so a mismatch is
/// a programmer error and throws.
/// </para>
/// </remarks>
internal sealed class LoanApplication
{
    private readonly List<LoanApplicationEvent> _uncommittedEvents = [];

    private LoanApplication(ApplicationSubmitted submitted)
    {
        Id = submitted.ApplicationId;
        Applicant = submitted.Applicant;
        Product = submitted.Product;
        Terms = submitted.Terms;
        SubmittedAt = submitted.OccurredAt;
        State = LoanApplicationState.Submitted;
        Version = 1;
    }

    private enum Step
    {
        Approve,
        Decline,
        SendToReview,
        Accept,
        Withdraw,
    }

    /// <summary>Gets the application id, which is also its history stream id.</summary>
    public LoanApplicationId Id { get; }

    /// <summary>Gets the applicant snapshot supplied at submission. It contains personal data.</summary>
    public ApplicantSnapshot Applicant { get; }

    /// <summary>Gets the product applied for.</summary>
    public ProductCode Product { get; }

    /// <summary>Gets the terms the applicant requested.</summary>
    public RequestedTerms Terms { get; }

    /// <summary>Gets when the application was submitted.</summary>
    public Instant SubmittedAt { get; }

    /// <summary>Gets the version of the decided terms: 0 until the first approval, then 1 more for each approval.</summary>
    public int TermsVersion { get; private set; }

    /// <summary>Gets the terms an underwriter approved, or <see langword="null"/> before an approval.</summary>
    public DecidedTerms? DecidedTerms { get; private set; }

    /// <summary>Gets the lifecycle state.</summary>
    public LoanApplicationState State { get; private set; }

    /// <summary>Gets the latest underwriting decision, or <see langword="null"/> before the first one.</summary>
    public UnderwritingDecision? Decision { get; private set; }

    /// <summary>Gets the number of events applied to the aggregate, committed or not: the stream version of its latest event.</summary>
    public int Version { get; private set; }

    /// <summary>Gets the events raised since the aggregate was created or last marked committed, oldest first.</summary>
    public IReadOnlyList<LoanApplicationEvent> UncommittedEvents => _uncommittedEvents;

    /// <summary>Starts an application from a submission.</summary>
    /// <param name="command">The submission.</param>
    /// <param name="id">The id for the new application.</param>
    /// <param name="now">The instant the command is handled.</param>
    /// <returns>The new application in <see cref="LoanApplicationState.Submitted"/>, or a <see cref="ValidationError"/>.</returns>
    internal static Result<LoanApplication, DomainError> Submit(SubmitApplication command, LoanApplicationId id, Instant now)
    {
        ArgumentNullException.ThrowIfNull(command);

        var error = LoanApplicationRules.ValidateSubmission(command.Applicant, command.Terms);
        if (error is not null)
        {
            return Result<LoanApplication, DomainError>.Failure(error);
        }

        var submitted = new ApplicationSubmitted(
            id, now, command.CommandId, command.CorrelationId, command.Applicant, command.Product, command.Terms);
        var application = new LoanApplication(submitted);
        application._uncommittedEvents.Add(submitted);
        return Result<LoanApplication, DomainError>.Success(application);
    }

    /// <summary>Rebuilds an application from its history, with no uncommitted events.</summary>
    /// <param name="history">The application's events, oldest first, starting with <see cref="ApplicationSubmitted"/>.</param>
    /// <returns>The application as the history left it.</returns>
    /// <exception cref="InvalidOperationException">
    /// The history is empty, does not start with a submission, mixes streams, or contains an event the state
    /// machine would not have allowed. Stored history comes from this aggregate, so that is a bug or corruption.
    /// </exception>
    internal static LoanApplication Rehydrate(IEnumerable<LoanApplicationEvent> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        LoanApplication? application = null;
        foreach (var @event in history)
        {
            if (application is null)
            {
                if (@event is not ApplicationSubmitted submitted)
                {
                    throw new InvalidOperationException(
                        $"A history must start with {nameof(ApplicationSubmitted)}, not {@event.GetType().Name}.");
                }

                application = new LoanApplication(submitted);
                continue;
            }

            application.EnsureReplayable(@event);
            application.Apply(@event);
        }

        return application ?? throw new InvalidOperationException("A history must contain at least one event.");
    }

    /// <summary>Records an underwriting decision.</summary>
    /// <param name="command">The decision. <c>FinalTerms</c> is required for an approval and absent otherwise.</param>
    /// <param name="now">The instant the command is handled.</param>
    /// <returns>
    /// Success, or an <see cref="InvalidStateTransition"/> when the state does not allow the outcome, or a
    /// <see cref="ValidationError"/> for a bad field.
    /// </returns>
    internal Result<Unit, DomainError> Decide(DecideApplication command, Instant now)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureSameApplication(command.Id);

        if (!TryGetStep(command.Outcome, out var step))
        {
            return Fail(new ValidationError(nameof(command.Outcome), "is not a known decision outcome"));
        }

        if (!IsAllowed(State, step))
        {
            return Reject(nameof(DecideApplication), step);
        }

        if (string.IsNullOrWhiteSpace(command.Rationale))
        {
            return Fail(new ValidationError(nameof(command.Rationale), "must not be empty"));
        }

        LoanApplicationEvent @event;
        if (step == Step.Approve)
        {
            if (command.FinalTerms is null)
            {
                return Fail(new ValidationError(nameof(command.FinalTerms), "is required to approve an application"));
            }

            var termsError = LoanApplicationRules.ValidateFinalTerms(command.FinalTerms);
            if (termsError is not null)
            {
                return Fail(termsError);
            }

            @event = new ApplicationApproved(
                Id, now, command.CommandId, command.CorrelationId, command.Rationale, command.FinalTerms, TermsVersion + 1);
        }
        else
        {
            if (command.FinalTerms is not null)
            {
                return Fail(new ValidationError(
                    nameof(command.FinalTerms),
                    $"must be absent when the outcome is {command.Outcome}"));
            }

            @event = step == Step.Decline
                ? new ApplicationDeclined(Id, now, command.CommandId, command.CorrelationId, command.Rationale)
                : new ApplicationSentToReview(Id, now, command.CommandId, command.CorrelationId, command.Rationale);
        }

        Raise(@event);
        return Result<Unit, DomainError>.Success(Unit.Value);
    }

    /// <summary>Records the borrower's acceptance of the current approved terms.</summary>
    /// <param name="command">The acceptance, carrying the terms version the borrower saw.</param>
    /// <param name="now">The instant the command is handled.</param>
    /// <returns>
    /// Success, or an <see cref="InvalidStateTransition"/> when the application is not approved, or a
    /// <see cref="StaleTermsVersion"/> when the terms were replaced after the version the borrower saw.
    /// </returns>
    internal Result<Unit, DomainError> AcceptTerms(AcceptTerms command, Instant now)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureSameApplication(command.Id);

        if (!IsAllowed(State, Step.Accept))
        {
            return Reject(nameof(AcceptTerms), Step.Accept);
        }

        if (command.TermsVersion != TermsVersion)
        {
            return Fail(new StaleTermsVersion(Expected: TermsVersion, Actual: command.TermsVersion));
        }

        // An approved application always has decided terms: ApplicationApproved is the only way into that state.
        Raise(new ApplicationAccepted(
            Id, now, command.CommandId, command.CorrelationId, Product, TermsVersion, DecidedTerms!));
        return Result<Unit, DomainError>.Success(Unit.Value);
    }

    /// <summary>Withdraws the application before the borrower has accepted terms.</summary>
    /// <param name="command">The withdrawal, with a non-empty reason.</param>
    /// <param name="now">The instant the command is handled.</param>
    /// <returns>
    /// Success, or an <see cref="InvalidStateTransition"/> when the application is accepted, declined or withdrawn,
    /// or a <see cref="ValidationError"/> for an empty reason.
    /// </returns>
    internal Result<Unit, DomainError> Withdraw(WithdrawApplication command, Instant now)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureSameApplication(command.Id);

        if (!IsAllowed(State, Step.Withdraw))
        {
            return Reject(nameof(WithdrawApplication), Step.Withdraw);
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Fail(new ValidationError(nameof(command.Reason), "must not be empty"));
        }

        Raise(new ApplicationWithdrawn(Id, now, command.CommandId, command.CorrelationId, command.Reason));
        return Result<Unit, DomainError>.Success(Unit.Value);
    }

    /// <summary>Clears <see cref="UncommittedEvents"/> once the handler has stored them.</summary>
    internal void MarkEventsCommitted() => _uncommittedEvents.Clear();

    // The transition table of docs/modules/origination.md ("States and transitions"), and nothing else, decides this.
    private static bool IsAllowed(LoanApplicationState from, Step step) =>
        step switch
        {
            Step.Approve or Step.Decline => from is LoanApplicationState.Submitted or LoanApplicationState.UnderReview,
            Step.SendToReview => from is LoanApplicationState.Submitted,
            Step.Accept => from is LoanApplicationState.Approved,
            Step.Withdraw => from is LoanApplicationState.Submitted
                or LoanApplicationState.UnderReview
                or LoanApplicationState.Approved,
            _ => false,
        };

    private static bool TryGetStep(DecisionOutcome outcome, out Step step)
    {
        switch (outcome)
        {
            case DecisionOutcome.Approve:
                step = Step.Approve;
                return true;
            case DecisionOutcome.Decline:
                step = Step.Decline;
                return true;
            case DecisionOutcome.HumanReview:
                step = Step.SendToReview;
                return true;
            default:
                step = default;
                return false;
        }
    }

    private static Result<Unit, DomainError> Fail(DomainError error) => Result<Unit, DomainError>.Failure(error);

    private Result<Unit, DomainError> Reject(string command, Step step)
    {
        var reason = State switch
        {
            LoanApplicationState.Declined or LoanApplicationState.Withdrawn =>
                $"the application is {State} and accepts no further commands",
            LoanApplicationState.AcceptedByBorrower =>
                "the borrower has accepted the terms and the handoff is already under way",
            LoanApplicationState.Approved => "the terms are already approved",
            LoanApplicationState.UnderReview when step == Step.SendToReview => "the application is already under review",
            _ when step == Step.Accept => "the application has no approved terms to accept",
            _ => $"the application is {State}",
        };

        return Fail(new InvalidStateTransition(State.ToString(), command, reason));
    }

    private void EnsureSameApplication(LoanApplicationId commandApplicationId)
    {
        if (commandApplicationId != Id)
        {
            throw new ArgumentException(
                $"The command is for application {commandApplicationId.Value}, but this is application {Id.Value}.",
                nameof(commandApplicationId));
        }
    }

    private void Raise(LoanApplicationEvent @event)
    {
        Apply(@event);
        _uncommittedEvents.Add(@event);
    }

    private void EnsureReplayable(LoanApplicationEvent @event)
    {
        if (@event.ApplicationId != Id)
        {
            throw new InvalidOperationException(
                $"Event {@event.GetType().Name} belongs to application {@event.ApplicationId.Value}, not {Id.Value}.");
        }

        var (step, expectedTermsVersion) = @event switch
        {
            ApplicationApproved approved => (Step.Approve, approved.TermsVersion - 1),
            ApplicationDeclined => (Step.Decline, TermsVersion),
            ApplicationSentToReview => (Step.SendToReview, TermsVersion),
            ApplicationAccepted accepted => (Step.Accept, accepted.TermsVersion),
            ApplicationWithdrawn => (Step.Withdraw, TermsVersion),
            _ => throw new InvalidOperationException($"Event {@event.GetType().Name} cannot follow the first event."),
        };

        if (!IsAllowed(State, step) || expectedTermsVersion != TermsVersion)
        {
            throw new InvalidOperationException(
                $"Event {@event.GetType().Name} is not valid for an application in state {State} at terms version {TermsVersion}.");
        }

        // The acceptance is the handoff to Servicing, so its payload must restate the approval it follows.
        // A stored acceptance of other terms or another product would leave the replayed state contradicting it.
        if (@event is ApplicationAccepted acceptance
            && (acceptance.Product != Product || !Equals(acceptance.AcceptedTerms, DecidedTerms)))
        {
            throw new InvalidOperationException(
                $"Event {nameof(ApplicationAccepted)} does not restate the approved product and terms of version {TermsVersion}.");
        }
    }

    private void Apply(LoanApplicationEvent @event)
    {
        switch (@event)
        {
            case ApplicationApproved approved:
                State = LoanApplicationState.Approved;
                TermsVersion = approved.TermsVersion;
                DecidedTerms = approved.FinalTerms;
                Decision = new UnderwritingDecision(DecisionOutcome.Approve, approved.Rationale, approved.OccurredAt);
                break;
            case ApplicationDeclined declined:
                State = LoanApplicationState.Declined;
                Decision = new UnderwritingDecision(DecisionOutcome.Decline, declined.Rationale, declined.OccurredAt);
                break;
            case ApplicationSentToReview review:
                State = LoanApplicationState.UnderReview;
                Decision = new UnderwritingDecision(DecisionOutcome.HumanReview, review.Rationale, review.OccurredAt);
                break;
            case ApplicationAccepted:
                State = LoanApplicationState.AcceptedByBorrower;
                break;
            case ApplicationWithdrawn:
                State = LoanApplicationState.Withdrawn;
                break;
            default:
                throw new InvalidOperationException($"Event {@event.GetType().Name} cannot be applied.");
        }

        Version++;
    }
}
