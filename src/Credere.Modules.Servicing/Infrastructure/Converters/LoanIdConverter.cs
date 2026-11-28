using Credere.Modules.Servicing.Contracts;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Credere.Modules.Servicing.Infrastructure.Converters;

/// <summary>Stores a <see cref="LoanId"/> as its <see cref="Guid"/>.</summary>
internal sealed class LoanIdConverter : ValueConverter<LoanId, Guid>
{
    /// <summary>Initializes a new instance of the <see cref="LoanIdConverter"/> class.</summary>
    public LoanIdConverter()
        : base(id => id.Value, stored => new LoanId(stored))
    {
    }
}
