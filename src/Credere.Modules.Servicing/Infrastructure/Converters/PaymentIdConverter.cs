using Credere.Modules.Servicing.Contracts;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Credere.Modules.Servicing.Infrastructure.Converters;

/// <summary>Stores a <see cref="PaymentId"/> as its <see cref="Guid"/>.</summary>
internal sealed class PaymentIdConverter : ValueConverter<PaymentId, Guid>
{
    /// <summary>Initializes a new instance of the <see cref="PaymentIdConverter"/> class.</summary>
    public PaymentIdConverter()
        : base(id => id.Value, stored => new PaymentId(stored))
    {
    }
}
