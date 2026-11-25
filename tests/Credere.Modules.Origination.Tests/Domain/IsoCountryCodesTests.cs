using Credere.Modules.Origination.Domain;
using FluentAssertions;
using Xunit;

namespace Credere.Modules.Origination.Tests.Domain;

public sealed class IsoCountryCodesTests
{
    // Source for every row: ISO 3166-1 alpha-2, "Officially assigned code elements", as transcribed in
    // IsoCountryCodes (English Wikipedia revision 1385919511, cross-checked against GeoNames countryInfo.txt).
    public static TheoryData<string> AssignedCodes() => new()
    {
        "AD", "AX", "BQ", "CW", "DE", "ES", "FR", "GB", "HK", "IE", "NL", "PL", "SS", "SX", "UA", "US", "ZW",
    };

    // Reserved, user-assigned or deleted codes, each with its status in the same article.
    public static TheoryData<string> UnassignedCodes() => new()
    {
        "UK", // Exceptionally reserved: the United Kingdom is GB.
        "EU", // Exceptionally reserved for the European Union.
        "XK", // User-assigned, in use for Kosovo.
        "XX", // User-assigned.
        "ZZ", // User-assigned.
        "AA", // User-assigned.
        "QM", // User-assigned.
        "AN", // Deleted: Netherlands Antilles.
        "CS", // Deleted: Serbia and Montenegro.
        "SU", // Exceptionally reserved, then deleted: USSR.
        "FX", // Reserved, then deleted: Metropolitan France.
    };

    [Fact]
    public void All_HoldsTheDocumentedNumberOfCodes()
    {
        IsoCountryCodes.All.Should().HaveCount(IsoCountryCodes.Count).And.OnlyHaveUniqueItems();
        IsoCountryCodes.Count.Should().Be(249);
    }

    [Fact]
    public void All_EveryCodeIsTwoUppercaseAsciiLetters()
    {
        IsoCountryCodes.All.Should().OnlyContain(code =>
            code.Length == 2 && code.All(character => character >= 'A' && character <= 'Z'));
    }

    [Theory]
    [MemberData(nameof(AssignedCodes))]
    public void IsAssigned_OfficiallyAssignedCode_IsTrue(string code) =>
        IsoCountryCodes.IsAssigned(code).Should().BeTrue();

    [Theory]
    [MemberData(nameof(UnassignedCodes))]
    public void IsAssigned_ReservedUserAssignedOrDeletedCode_IsFalse(string code) =>
        IsoCountryCodes.IsAssigned(code).Should().BeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("D")]
    [InlineData("DEU")]
    [InlineData("de")]
    [InlineData("De")]
    [InlineData(" DE")]
    [InlineData("DE ")]
    public void IsAssigned_MalformedOrWrongCaseInput_IsFalse(string? code) =>
        IsoCountryCodes.IsAssigned(code).Should().BeFalse();
}
