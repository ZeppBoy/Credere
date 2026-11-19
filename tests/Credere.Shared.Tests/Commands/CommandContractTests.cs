using Credere.Shared.Commands;
using Xunit;

namespace Credere.Shared.Tests.Commands;

/// <summary>Tests the shape of <see cref="ICommand"/>.</summary>
public sealed class CommandContractTests
{
    private sealed record SampleCommand(Guid CommandId, string Payload) : ICommand;

    [Fact]
    public void Command_exposesItsCommandId()
    {
        var id = Guid.NewGuid();

        ICommand command = new SampleCommand(id, "x");

        Assert.Equal(id, command.CommandId);
    }

    [Fact]
    public void Commands_withTheSameCommandId_areDistinguishedFromOtherSubmissionsByThatIdOnly()
    {
        // Idempotency (ADR-0007) keys on CommandId, so two submissions of one command share it.
        var id = Guid.NewGuid();
        ICommand first = new SampleCommand(id, "x");
        ICommand resubmitted = new SampleCommand(id, "x");
        ICommand other = new SampleCommand(Guid.NewGuid(), "x");

        Assert.Equal(first.CommandId, resubmitted.CommandId);
        Assert.NotEqual(first.CommandId, other.CommandId);
    }
}
