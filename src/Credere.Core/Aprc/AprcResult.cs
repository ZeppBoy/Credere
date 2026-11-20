using Credere.Core.ValueObjects;

namespace Credere.Core.Aprc;

/// <summary>The annual percentage rate of charge and its calculation details.</summary>
/// <param name="Rate">The computed rate as a fraction, before disclosure rounding.</param>
/// <param name="Disclosed">The APRC as percent units, rounded half up to two decimal places.</param>
/// <param name="Iterations">The number of bisection iterations used to find the rate.</param>
/// <param name="FinalResidual">The present value of the drawdown less the present value of the payments, in currency units.</param>
public sealed record AprcResult(decimal Rate, Percentage Disclosed, int Iterations, decimal FinalResidual);
