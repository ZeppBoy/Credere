using Credere.Core.Results;
using Credere.Modules.Servicing.Application.Queries.QuotePayoff;
using Credere.Modules.Servicing.Contracts;
using Credere.Modules.Servicing.Domain;
using FluentAssertions;
using Xunit;
using static Credere.Modules.Servicing.Tests.Domain.AccrualSamples;

namespace Credere.Modules.Servicing.Tests.Domain;

public sealed class PayoffCalculationTests
{
    private static readonly LoanId Loan = new(Guid.NewGuid());

    // D5 and PayoffCalculation/reference-vectors.md, PO-01 through PO-08.
    public static TheoryData<string, string, string, string, decimal, decimal, decimal, decimal> ReferenceCases => new()
    {
        { "PO-01", "2026-01-15", "2026-01-15", "2026-01-15", 10000m, 0m, 0m, 10000m },
        { "PO-02", "2026-01-15", "2026-01-15", "2026-02-14", 10000m, 0m, 40.28m, 10040.28m },
        { "PO-03", "2026-01-15", "2026-01-15", "2026-02-15", 10000m, 0m, 41.67m, 10041.67m },
        { "PO-04", "2026-01-15", "2026-01-15", "2026-02-16", 10000m, 41.67m, 1.39m, 10043.06m },
        { "PO-05", "2026-01-15", "2026-01-15", "2026-03-15", 10000m, 41.67m, 41.67m, 10083.34m },
        { "PO-06", "2026-01-15", "2026-01-15", "2026-01-20", 10000m, 0m, 6.94m, 10006.94m },
        { "PO-07", "2026-01-17", "2026-01-15", "2026-01-20", 10000m, 0m, 4.16m, 10004.16m },
        { "PO-08", "2026-01-17", "2026-01-17", "2026-01-20", 5000m, 0m, 2.08m, 5002.08m },
    };

    [Theory]
    [MemberData(nameof(ReferenceCases))]
    public void Quote_ReferencePosition_MatchesD5(
        string caseId, string postedDate, string segmentStart, string payoffDate,
        decimal principal, decimal pastDue, decimal current, decimal expectedTotal)
    {
        var query = new QuotePayoff(Loan, Date(payoffDate));
        var position = At(postedDate, segmentStart);

        var quote = PayoffCalculation.Quote(query, Euro(principal), Euro(0m), position, Rate, Schedule, Calculator).Value;

        var expected = new PayoffQuote(Loan, query.PayoffDate, Euro(principal), Euro(pastDue), Euro(current), Euro(0m), Euro(expectedTotal));
        quote.Should().Be(expected, "{0} specifies each balance on the quote date", caseId);
    }

    [Fact]
    public void Quote_DateBeforePostedAccrual_ReturnsValidationError()
    {
        // PO-09 uses the posted position from IA-05.
        var query = new QuotePayoff(Loan, Date("2026-02-15"));
        var position = At("2026-02-16", "2026-02-15", current: 1.39m, pastDue: 41.67m);

        var result = PayoffCalculation.Quote(query, Principal, Euro(0m), position, Rate, Schedule, Calculator);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeOfType<ValidationError>().Which.Field.Should().Be("payoffDate");
    }

    [Fact]
    public void ValidateQuotedTotal_InterveningPayment_ReturnsStaleQuote()
    {
        // PO-10: the principal payment in IA-08 supersedes the PO-06 quote.
        var query = new QuotePayoff(Loan, Date("2026-01-20"));
        var original = PayoffCalculation.Quote(query, Principal, Euro(0m), At(), Rate, Schedule, Calculator).Value;
        var recomputed = PayoffCalculation.Quote(
            query, Euro(5000m), Euro(0m), At("2026-01-17", "2026-01-17"), Rate, Schedule, Calculator).Value;

        var result = PayoffCalculation.ValidateQuotedTotal(recomputed, original.Total);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(new PayoffQuoteStale(Euro(10006.94m), Euro(5002.08m)));
    }

    [Fact]
    public void ValidateQuotedTotal_UnchangedLoan_AcceptsTheExactAmountNeededToClose()
    {
        // PO-11: the aggregate can pass this exact total to the waterfall.
        var query = new QuotePayoff(Loan, Date("2026-01-20"));
        var recomputed = PayoffCalculation.Quote(
            query, Euro(5000m), Euro(0m), At("2026-01-17", "2026-01-17"), Rate, Schedule, Calculator).Value;
        var quoted = Euro(5002.08m);

        var result = PayoffCalculation.ValidateQuotedTotal(recomputed, quoted);

        result.IsSuccess.Should().BeTrue();
        (quoted - recomputed.Fees - recomputed.PastDueInterest - recomputed.CurrentInterest - recomputed.Principal)
            .Should().Be(Euro(0m));
    }
}
