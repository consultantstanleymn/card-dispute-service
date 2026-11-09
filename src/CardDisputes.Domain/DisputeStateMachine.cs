namespace CardDisputes.Domain;

public static class DisputeStateMachine
{
    public static bool CanTransition(DisputeStatus from, DisputeStatus to) => throw new NotImplementedException();

    public static void EnsureTransition(DisputeStatus from, DisputeStatus to) => throw new NotImplementedException();

    public static void EnsureSubmit(DisputeStatus from, int evidenceCount, DateOnly deadline, DateOnly todayUtc) =>
        throw new NotImplementedException();
}
