namespace CardDisputes.Domain;

public static class ResponseDeadline
{
    public static int Days(ReasonCode reason) => reason switch
    {
        ReasonCode.FraudCardAbsent => 30,
        ReasonCode.GoodsNotReceived => 20,
        ReasonCode.DuplicateCharge => 20,
        ReasonCode.CancelledRecurring => 20,
        ReasonCode.CreditNotProcessed => 20,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown reason code.")
    };

    public static DateOnly For(ReasonCode reason, DateTimeOffset openedAt)
    {
        var openedOn = DateOnly.FromDateTime(openedAt.UtcDateTime);
        return openedOn.AddDays(Days(reason));
    }
}
