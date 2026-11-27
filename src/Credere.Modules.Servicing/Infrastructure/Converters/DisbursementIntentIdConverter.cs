using Credere.Modules.Servicing.Contracts;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Credere.Modules.Servicing.Infrastructure.Converters;

/// <summary>Stores a <see cref="DisbursementIntentId"/> as its <see cref="Guid"/>.</summary>
internal sealed class DisbursementIntentIdConverter : ValueConverter<DisbursementIntentId, Guid>
{
    /// <summary>Initializes a new instance of the <see cref="DisbursementIntentIdConverter"/> class.</summary>
    public DisbursementIntentIdConverter()
        : base(id => id.Value, stored => new DisbursementIntentId(stored))
    {
    }
}
