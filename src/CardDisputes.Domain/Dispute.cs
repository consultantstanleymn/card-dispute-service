namespace CardDisputes.Domain;

public sealed class Dispute
{
    private readonly List<Guid> _evidenceIds = new();

    public Dispute(Guid id, ReasonCode reason, Money amount, DateTimeOffset openedAt)
    {
        Id = id;
        Reason = reason;
        Amount = amount;
        OpenedAt = openedAt;
        ResponseDeadline = global::CardDisputes.Domain.ResponseDeadline.For(reason, openedAt);
        Status = DisputeStatus.Opened;
    }

    public Guid Id { get; }

    public DisputeStatus Status { get; private set; }

    public ReasonCode Reason { get; }

    public Money Amount { get; }

    public DateTimeOffset OpenedAt { get; }

    public DateOnly ResponseDeadline { get; }

    public int EvidenceCount => _evidenceIds.Count;

    public void AttachEvidence(Guid evidenceId)
    {
        if (Status is not (DisputeStatus.Opened or DisputeStatus.EvidenceSubmitted))
        {
            throw new InvalidOperationException($"Cannot attach evidence to a dispute in status {Status}.");
        }

        _evidenceIds.Add(evidenceId);
    }

    public void Submit(DateOnly todayUtc) => Apply(DisputeAction.Submit, todayUtc);

    public void Decide(DisputeStatus outcome, DateOnly todayUtc) => Apply(
        outcome switch
        {
            DisputeStatus.Won => DisputeAction.DecideWon,
            DisputeStatus.Lost => DisputeAction.DecideLost,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Outcome must be Won or Lost.")
        },
        todayUtc);

    public void Withdraw(DateOnly todayUtc) => Apply(DisputeAction.Withdraw, todayUtc);

    private void Apply(DisputeAction action, DateOnly todayUtc)
    {
        var transition = NextStatus(action, todayUtc);
        if (transition.Status is not { } nextStatus)
        {
            throw new InvalidOperationException($"Action {action} is not allowed: {transition.Rejection}.");
        }

        Status = nextStatus;
    }

    private DisputeTransition NextStatus(DisputeAction action, DateOnly todayUtc) =>
        DisputeStateMachine.Next(Status, action, EvidenceCount > 0, todayUtc, ResponseDeadline);
}
