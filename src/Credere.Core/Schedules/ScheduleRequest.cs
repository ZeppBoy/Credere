using Credere.Core.Rates;
using Credere.Core.Results;
using Credere.Core.ValueObjects;
using NodaTime;

namespace Credere.Core.Schedules;

/// <summary>
/// What a generator needs to build a schedule (docs/domain/schedules.md, "Public contract"). Create one with
/// <see cref="Create"/>, which checks the combination of dates, term and frequency.
/// </summary>
public sealed record ScheduleRequest
{
    private ScheduleRequest(
        Money principal,
        InterestRate rate,
        LoanTerm term,
        LocalDate disbursementDate,
        LocalDate firstPaymentDate,
        PaymentFrequency paymentFrequency)
    {
        Principal = principal;
        Rate = rate;
        Term = term;
        DisbursementDate = disbursementDate;
        FirstPaymentDate = firstPaymentDate;
        PaymentFrequency = paymentFrequency;
    }

    /// <summary>Gets the amount lent.</summary>
    public Money Principal { get; }

    /// <summary>Gets the interest rate.</summary>
    public InterestRate Rate { get; }

    /// <summary>Gets the loan term in whole months.</summary>
    public LoanTerm Term { get; }

    /// <summary>Gets the date the principal is paid out, the start of the first interest period.</summary>
    public LocalDate DisbursementDate { get; }

    /// <summary>Gets the date of the first payment, the end of the first interest period. Later dates count from it.</summary>
    public LocalDate FirstPaymentDate { get; }

    /// <summary>Gets how often a payment falls due.</summary>
    public PaymentFrequency PaymentFrequency { get; }

    /// <summary>Creates a request from untrusted input.</summary>
    /// <param name="principal">The amount lent.</param>
    /// <param name="rate">The interest rate.</param>
    /// <param name="term">The loan term.</param>
    /// <param name="disbursementDate">The disbursement date.</param>
    /// <param name="firstPaymentDate">The first payment date, strictly after <paramref name="disbursementDate"/>.</param>
    /// <param name="paymentFrequency">The payment frequency.</param>
    /// <returns>
    /// The request, or a <see cref="ValidationError"/> on field <c>paymentFrequency</c> (not a defined value),
    /// <c>firstPaymentDate</c> (not after the disbursement date) or <c>term</c> (not a whole number of payment
    /// periods, for example 10 months with quarterly payments). There is no maximum length of the first period in M1.
    /// </returns>
    /// <exception cref="ArgumentNullException">A reference argument is <see langword="null"/>, which is a programmer error.</exception>
    public static Result<ScheduleRequest, DomainError> Create(
        Money principal,
        InterestRate rate,
        LoanTerm term,
        LocalDate disbursementDate,
        LocalDate firstPaymentDate,
        PaymentFrequency paymentFrequency)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(rate);
        ArgumentNullException.ThrowIfNull(term);

        if (!Enum.IsDefined(paymentFrequency))
        {
            return Fail("paymentFrequency", "must be Monthly or Quarterly");
        }

        if (firstPaymentDate <= disbursementDate)
        {
            return Fail("firstPaymentDate", "must be after the disbursement date");
        }

        if (term.Months % SchedulePeriods.MonthsPerPeriod(paymentFrequency) != 0)
        {
            return Fail("term", "must be a whole number of payment periods (a multiple of 3 months for quarterly payments)");
        }

        return Result<ScheduleRequest, DomainError>.Success(
            new ScheduleRequest(principal, rate, term, disbursementDate, firstPaymentDate, paymentFrequency));
    }

    private static Result<ScheduleRequest, DomainError> Fail(string field, string reason) =>
        Result<ScheduleRequest, DomainError>.Failure(new ValidationError(field, reason));
}
