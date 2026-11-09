#pragma warning disable CA1707
using CardDisputes.Domain;

namespace CardDisputes.UnitTests;

public class DisputeStateMachineTests
{
    private static readonly DateOnly Deadline = new(2026, 2, 1);
    private static readonly DateOnly BeforeDeadline = new(2026, 1, 15);

    [Fact]
    public void Opened_To_EvidenceSubmitted_Needs_Evidence()
    {
        Assert.Throws<InvalidOperationException>(
            () => DisputeStateMachine.EnsureSubmit(DisputeStatus.Opened, 0, Deadline, BeforeDeadline));

        DisputeStateMachine.EnsureSubmit(DisputeStatus.Opened, 1, Deadline, BeforeDeadline);
    }

    [Fact]
    public void Opened_To_Withdrawn_Allowed()
    {
        DisputeStateMachine.EnsureTransition(DisputeStatus.Opened, DisputeStatus.Withdrawn);
    }

    [Fact]
    public void EvidenceSubmitted_To_Won_And_Lost_Allowed()
    {
        DisputeStateMachine.EnsureTransition(DisputeStatus.EvidenceSubmitted, DisputeStatus.Won);
        DisputeStateMachine.EnsureTransition(DisputeStatus.EvidenceSubmitted, DisputeStatus.Lost);
    }

    [Theory]
    [InlineData(DisputeStatus.Won)]
    [InlineData(DisputeStatus.Lost)]
    [InlineData(DisputeStatus.Withdrawn)]
    public void Terminal_States_Reject_All_Transitions(DisputeStatus terminal)
    {
        foreach (var target in Enum.GetValues<DisputeStatus>())
        {
            Assert.Throws<InvalidOperationException>(
                () => DisputeStateMachine.EnsureTransition(terminal, target));
        }
    }

    [Fact]
    public void Submit_After_Deadline_Rejected()
    {
        var dayAfterDeadline = Deadline.AddDays(1);

        Assert.Throws<InvalidOperationException>(
            () => DisputeStateMachine.EnsureSubmit(DisputeStatus.Opened, 1, Deadline, dayAfterDeadline));
    }
}
