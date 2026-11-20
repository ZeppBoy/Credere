using Credere.Core.Results;

namespace Credere.Core.Aprc;

/// <summary>Calculates the annual percentage rate of charge for a consumer credit agreement.</summary>
public interface IAprcCalculator
{
    /// <summary>Calculates the APRC from the drawdown and dated borrower payments.</summary>
    /// <param name="request">The drawdown and payments in a single currency, including applicable charges.</param>
    /// <returns>The APRC and solver details, or a validation error for an invalid or unsolvable request.</returns>
    Result<AprcResult, DomainError> Calculate(AprcRequest request);
}
