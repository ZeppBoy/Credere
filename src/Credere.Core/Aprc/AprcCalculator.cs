using Credere.Core.Results;
using Credere.Core.ValueObjects;
using NodaTime;

namespace Credere.Core.Aprc;

/// <summary>
/// Calculates the APRC under Directive (EU) 2023/2225, Article 30 and Annex III, Part I.
/// </summary>
/// <remarks>
/// The calculation uses the first drawdown date and the intervals under remarks (b) and (c). Disclosure rounds
/// the percentage half up under remark (d). End-to-end reference cases are Directive 98/7/EC, Annex II, Part B,
/// examples 1–4.
/// </remarks>
public sealed class AprcCalculator : IAprcCalculator
{
    private static readonly EuCcdAnnexIII TimeBasis = new();
    private const string DrawdownsField = "drawdowns";
    private const string PaymentsField = "borrowerPayments";

    /// <inheritdoc />
    public Result<AprcResult, DomainError> Calculate(AprcRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Drawdowns is not { Count: 1 })
        {
            return Invalid(DrawdownsField, "exactly one drawdown is required");
        }

        var drawdown = request.Drawdowns[0];
        if (drawdown?.Amount is not { Amount: > 0m } drawdownAmount)
        {
            return Invalid(DrawdownsField, "the drawdown amount must be positive");
        }

        if (request.BorrowerPayments is not { Count: > 0 } borrowerPayments)
        {
            return Invalid(PaymentsField, "at least one borrower payment is required");
        }

        var drawdownDate = drawdown.Date.WithCalendar(CalendarSystem.Iso);
        var payments = new (decimal Amount, decimal Years)[borrowerPayments.Count];
        for (var index = 0; index < payments.Length; index++)
        {
            var payment = borrowerPayments[index];
            if (payment?.Amount is not { Amount: >= 0m } paymentAmount)
            {
                return Invalid(PaymentsField, "each payment must have a non-negative amount");
            }

            if (!StringComparer.Ordinal.Equals(paymentAmount.Currency, drawdownAmount.Currency))
            {
                return Invalid(PaymentsField, "all payments must use the drawdown currency");
            }

            var paymentDate = payment.Date.WithCalendar(CalendarSystem.Iso);
            if (paymentDate < drawdownDate)
            {
                return Invalid(PaymentsField, "a payment precedes the drawdown");
            }

            payments[index] = (paymentAmount.Amount, TimeBasis.Fraction(drawdownDate, paymentDate));
        }

        return AprcSolver.Solve(drawdownAmount.Amount, payments).Map(static solution => new AprcResult(
            solution.Rate,
            new Percentage(solution.DisclosedPercent),
            solution.Iterations,
            solution.FinalResidual));
    }

    private static Result<AprcResult, DomainError> Invalid(string field, string reason) =>
        Result<AprcResult, DomainError>.Failure(new ValidationError(field, reason));
}
