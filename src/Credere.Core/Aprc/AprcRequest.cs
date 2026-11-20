namespace Credere.Core.Aprc;

/// <summary>The dated cash flows used to calculate the annual percentage rate of charge.</summary>
/// <param name="Drawdowns">The single net drawdown, after any charges withheld by the creditor.</param>
/// <param name="BorrowerPayments">The borrower’s repayments and applicable charges in the drawdown currency.</param>
public sealed record AprcRequest(IReadOnlyList<CashFlow> Drawdowns, IReadOnlyList<CashFlow> BorrowerPayments);
