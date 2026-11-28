using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Credere.Modules.Origination.Infrastructure.Converters;

/// <summary>
/// Stores a <see cref="decimal"/> as its exact invariant text, for example <c>100.50</c>, in a <c>varchar(32)</c>.
/// </summary>
/// <remarks>
/// <para>
/// A <c>decimal(p,s)</c> column cannot hold every value the domain accepts. The SQL Server client converts a .NET
/// decimal to the column's scale before sending it, and that scaling overflows the 96-bit coefficient: at scale 28 any
/// value from about 7.92 up fails, and at scale 2 so does an amount near <see cref="decimal.MaxValue"/>. A narrower
/// column would round or reject values the aggregate accepted, so the table keeps the exact text instead, with the
/// scale as it was written. The history payload keeps the same digits as a JSON number.
/// </para>
/// <para>
/// The cost is that the database cannot sort or sum these columns. Nothing in Origination does, and reporting reads
/// events, not this table. A column that must be queried numerically needs a range and a scale the owner agrees,
/// validated before the write.
/// </para>
/// </remarks>
internal sealed class DecimalTextConverter : ValueConverter<decimal, string>
{
    /// <summary>The widest text a decimal can need: 28 digits, a sign and a decimal point, with room to spare.</summary>
    internal const int MaxLength = 32;

    /// <summary>Initializes a new instance of the <see cref="DecimalTextConverter"/> class.</summary>
    public DecimalTextConverter()
        : base(
            value => value.ToString(CultureInfo.InvariantCulture),
            stored => decimal.Parse(stored, NumberStyles.Number, CultureInfo.InvariantCulture))
    {
    }
}
