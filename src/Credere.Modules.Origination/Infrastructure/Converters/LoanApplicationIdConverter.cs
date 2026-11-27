using Credere.Modules.Origination.Contracts;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Credere.Modules.Origination.Infrastructure.Converters;

/// <summary>Stores a <see cref="LoanApplicationId"/> as its <see cref="Guid"/>.</summary>
internal sealed class LoanApplicationIdConverter : ValueConverter<LoanApplicationId, Guid>
{
    /// <summary>Initializes a new instance of the <see cref="LoanApplicationIdConverter"/> class.</summary>
    public LoanApplicationIdConverter()
        : base(id => id.Value, stored => new LoanApplicationId(stored))
    {
    }
}
