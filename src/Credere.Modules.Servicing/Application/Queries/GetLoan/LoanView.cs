using Credere.Core.Schedules;
using Credere.Core.ValueObjects;
using Credere.Modules.Servicing.Contracts;
using NodaTime;

namespace Credere.Modules.Servicing.Application.Queries.GetLoan;

/// <summary>Describes the current balances, terms, dates and state of a loan.</summary>
/// <param name="Id">The loan identifier.</param>
/// <param name="SourceReference">The originating application's opaque reference.</param>
/// <param name="TermsVersion">The version of the approved terms.</param>
/// <param name="Product">The product key.</param>
/// <param name="Principal">The principal amount.</param>
/// <param name="RemainingPrincipal">The outstanding principal.</param>
/// <param name="FeesOutstanding">The outstanding fees.</param>
/// <param name="PastDueInterest">The outstanding past-due interest.</param>
/// <param name="CurrentInterest">The outstanding current interest.</param>
/// <param name="NominalRate">The annual nominal interest rate.</param>
/// <param name="DayCountConvention">The name of the day-count convention.</param>
/// <param name="Term">The term in whole months.</param>
/// <param name="Frequency">The scheduled payment frequency.</param>
/// <param name="Repayment">The repayment schedule type.</param>
/// <param name="DisbursementDate">The business date of the disbursement.</param>
/// <param name="FirstPaymentDate">The date of the first scheduled payment.</param>
/// <param name="AccruedThroughDate">The exclusive end date of posted interest.</param>
/// <param name="PastDueThreshold">The days-past-due threshold for entering PastDue.</param>
/// <param name="DefaultThreshold">The days-past-due threshold for entering Defaulted.</param>
/// <param name="State">The current or resulting state.</param>
/// <param name="DaysPastDue">The days past due.</param>
public sealed record LoanView(
    LoanId Id,
    Guid SourceReference,
    int TermsVersion,
    string Product,
    Money Principal,
    Money RemainingPrincipal,
    Money FeesOutstanding,
    Money PastDueInterest,
    Money CurrentInterest,
    Percentage NominalRate,
    string DayCountConvention,
    LoanTerm Term,
    PaymentFrequency Frequency,
    RepaymentType Repayment,
    LocalDate DisbursementDate,
    LocalDate FirstPaymentDate,
    LocalDate AccruedThroughDate,
    int PastDueThreshold,
    int DefaultThreshold,
    LoanState State,
    int? DaysPastDue);
