namespace CardDisputes.Domain;

public sealed record DisputeTransition
{
    private DisputeTransition(DisputeStatus? status, TransitionRejection? rejection)
    {
        Status = status;
        Rejection = rejection;
    }

    public DisputeStatus? Status { get; }

    public TransitionRejection? Rejection { get; }

    public bool IsAllowed => Status.HasValue;

    public static DisputeTransition Allowed(DisputeStatus status) => new(status, null);

    public static DisputeTransition Rejected(TransitionRejection reason) => new(null, reason);
}
