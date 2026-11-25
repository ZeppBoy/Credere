using Credere.Core.Results;

namespace Credere.Modules.Origination.Contracts;

/// <summary>Indicates that the terms version supplied for acceptance is obsolete.</summary>
/// <param name="Expected">The application's current terms version.</param>
/// <param name="Actual">The version supplied for acceptance.</param>
public sealed record StaleTermsVersion(int Expected, int Actual)
    : DomainError("stale-terms-version", "The terms were replaced after the version the borrower saw.");
