namespace CardDisputes.Domain;

public static class DisputeStateMachine
{
    public static bool CanTransition(DisputeStatus from, DisputeStatus to) => (from, to) switch
    {
        (DisputeStatus.Opened, DisputeStatus.EvidenceSubmitted) => true,
        (DisputeStatus.Opened, DisputeStatus.Withdrawn) => true,
        (DisputeStatus.EvidenceSubmitted, DisputeStatus.Won) => true,
        (DisputeStatus.EvidenceSubmitted, DisputeStatus.Lost) => true,
        (DisputeStatus.EvidenceSubmitted, DisputeStatus.Withdrawn) => true,
        _ => false
    };

    public static void EnsureTransition(DisputeStatus from, DisputeStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException($"Transition from {from} to {to} is not allowed.");
        }
    }

    public static void EnsureSubmit(DisputeStatus from, int evidenceCount, DateOnly deadline, DateOnly todayUtc)
    {
        var transition = Next(from, DisputeAction.Submit, evidenceCount > 0, todayUtc, deadline);
        if (!transition.IsAllowed)
        {
            throw new InvalidOperationException($"Submit is not allowed: {transition.Rejection}.");
        }
    }

    public static DisputeTransition Next(
        DisputeStatus current,
        DisputeAction action,
        bool hasEvidence,
        DateOnly nowUtc,
        DateOnly deadline)
    {
        var target = Target(action);

        if (action == DisputeAction.Submit)
        {
            if (nowUtc > deadline)
            {
                return DisputeTransition.Rejected(TransitionRejection.DeadlinePassed);
            }

            if (!hasEvidence)
            {
                return DisputeTransition.Rejected(TransitionRejection.NoEvidence);
            }
        }

        return CanTransition(current, target)
            ? DisputeTransition.Allowed(target)
            : DisputeTransition.Rejected(TransitionRejection.IllegalTransition);
    }

    private static DisputeStatus Target(DisputeAction action) => action switch
    {
        DisputeAction.Submit => DisputeStatus.EvidenceSubmitted,
        DisputeAction.DecideWon => DisputeStatus.Won,
        DisputeAction.DecideLost => DisputeStatus.Lost,
        DisputeAction.Withdraw => DisputeStatus.Withdrawn,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown action.")
    };
}
