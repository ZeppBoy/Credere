using System.Collections.Frozen;

namespace Credere.Modules.Origination.Domain;

/// <summary>
/// The officially assigned ISO 3166-1 alpha-2 country codes, pinned as a constant set so a submission is checked
/// against a fixed list and not against a library that changes under it (AGENTS.md §4).
/// </summary>
/// <remarks>
/// <para>
/// Source: the 249 "officially assigned" elements of ISO 3166-1 alpha-2, read from the English Wikipedia article
/// "ISO 3166-1 alpha-2", revision 1385919511 of 2026-11-15, section "Officially assigned code elements". The ISO
/// Online Browsing Platform (https://www.iso.org/obp) is the authority but can't be read by a script, so this list
/// is a transcription of that secondary source. It was cross-checked on 2026-11-25 against the GeoNames country
/// list (https://download.geonames.org/export/dump/countryInfo.txt): the two agree on all 249 codes, and GeoNames
/// adds only <c>XK</c> (user-assigned) and the deleted codes <c>AN</c> and <c>CS</c>.
/// </para>
/// <para>
/// Codes that ISO has not officially assigned are rejected, as the owner decided on PR #90. That covers the
/// user-assigned codes (<c>AA</c>, <c>QM</c> to <c>QZ</c>, <c>XA</c> to <c>XZ</c>, <c>ZZ</c>, so <c>XK</c> for
/// Kosovo is rejected too), the exceptionally, transitionally and indeterminately reserved codes (<c>EU</c>,
/// <c>UK</c> and others) and the deleted codes. The United Kingdom is <c>GB</c>. ISO changes this list rarely; when
/// it does, update the set, the revision above and the tests in the same change.
/// </para>
/// </remarks>
internal static class IsoCountryCodes
{
    /// <summary>The number of officially assigned codes in the pinned list.</summary>
    internal const int Count = 249;

    private static readonly FrozenSet<string> Assigned = new[]
    {
        "AD", "AE", "AF", "AG", "AI", "AL", "AM", "AO", "AQ", "AR", "AS", "AT",
        "AU", "AW", "AX", "AZ", "BA", "BB", "BD", "BE", "BF", "BG", "BH", "BI",
        "BJ", "BL", "BM", "BN", "BO", "BQ", "BR", "BS", "BT", "BV", "BW", "BY",
        "BZ", "CA", "CC", "CD", "CF", "CG", "CH", "CI", "CK", "CL", "CM", "CN",
        "CO", "CR", "CU", "CV", "CW", "CX", "CY", "CZ", "DE", "DJ", "DK", "DM",
        "DO", "DZ", "EC", "EE", "EG", "EH", "ER", "ES", "ET", "FI", "FJ", "FK",
        "FM", "FO", "FR", "GA", "GB", "GD", "GE", "GF", "GG", "GH", "GI", "GL",
        "GM", "GN", "GP", "GQ", "GR", "GS", "GT", "GU", "GW", "GY", "HK", "HM",
        "HN", "HR", "HT", "HU", "ID", "IE", "IL", "IM", "IN", "IO", "IQ", "IR",
        "IS", "IT", "JE", "JM", "JO", "JP", "KE", "KG", "KH", "KI", "KM", "KN",
        "KP", "KR", "KW", "KY", "KZ", "LA", "LB", "LC", "LI", "LK", "LR", "LS",
        "LT", "LU", "LV", "LY", "MA", "MC", "MD", "ME", "MF", "MG", "MH", "MK",
        "ML", "MM", "MN", "MO", "MP", "MQ", "MR", "MS", "MT", "MU", "MV", "MW",
        "MX", "MY", "MZ", "NA", "NC", "NE", "NF", "NG", "NI", "NL", "NO", "NP",
        "NR", "NU", "NZ", "OM", "PA", "PE", "PF", "PG", "PH", "PK", "PL", "PM",
        "PN", "PR", "PS", "PT", "PW", "PY", "QA", "RE", "RO", "RS", "RU", "RW",
        "SA", "SB", "SC", "SD", "SE", "SG", "SH", "SI", "SJ", "SK", "SL", "SM",
        "SN", "SO", "SR", "SS", "ST", "SV", "SX", "SY", "SZ", "TC", "TD", "TF",
        "TG", "TH", "TJ", "TK", "TL", "TM", "TN", "TO", "TR", "TT", "TV", "TW",
        "TZ", "UA", "UG", "UM", "US", "UY", "UZ", "VA", "VC", "VE", "VG", "VI",
        "VN", "VU", "WF", "WS", "YE", "YT", "ZA", "ZM", "ZW",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Gets every code in the pinned list.</summary>
    internal static IReadOnlyCollection<string> All => Assigned;

    /// <summary>Tells whether <paramref name="code"/> is an officially assigned ISO 3166-1 alpha-2 code.</summary>
    /// <param name="code">The candidate code. Matching is exact and case-sensitive, so <c>de</c> is not <c>DE</c>.</param>
    /// <returns><see langword="true"/> when the code is in the pinned list.</returns>
    internal static bool IsAssigned(string? code) => code is not null && Assigned.Contains(code);
}
