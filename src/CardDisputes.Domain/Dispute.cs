namespace CardDisputes.Domain;

public sealed class Dispute
{
    public Dispute(Guid id, ReasonCode reason, Money amount, DateTimeOffset openedAt) =>
        throw new NotImplementedException();

    public Guid Id => throw new NotImplementedException();

    public DisputeStatus Status => throw new NotImplementedException();

    public ReasonCode Reason => throw new NotImplementedException();

    public Money Amount => throw new NotImplementedException();

    public DateTimeOffset OpenedAt => throw new NotImplementedException();

    public DateOnly ResponseDeadline => throw new NotImplementedException();

    public int EvidenceCount => throw new NotImplementedException();
}
