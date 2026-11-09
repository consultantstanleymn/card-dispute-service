#pragma warning disable CA1707
using CardDisputes.Domain;

namespace CardDisputes.UnitTests;

public class ResponseDeadlineTests
{
    private static readonly DateTimeOffset OpenedAt = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Fraud_Deadline_Is_30_Days()
    {
        Assert.Equal(30, ResponseDeadline.Days(ReasonCode.FraudCardAbsent));
        Assert.Equal(new DateOnly(2026, 1, 31), ResponseDeadline.For(ReasonCode.FraudCardAbsent, OpenedAt));
    }

    [Theory]
    [InlineData(ReasonCode.GoodsNotReceived)]
    [InlineData(ReasonCode.DuplicateCharge)]
    [InlineData(ReasonCode.CancelledRecurring)]
    [InlineData(ReasonCode.CreditNotProcessed)]
    public void Other_Reasons_Deadline_Is_20_Days(ReasonCode reason)
    {
        Assert.Equal(20, ResponseDeadline.Days(reason));
        Assert.Equal(new DateOnly(2026, 1, 21), ResponseDeadline.For(reason, OpenedAt));
    }

    [Fact]
    public void Deadline_Uses_Utc_Date()
    {
        var openedAtUtc = new DateTimeOffset(2026, 1, 1, 23, 30, 0, TimeSpan.Zero);
        var openedAtOtherOffset = openedAtUtc.ToOffset(TimeSpan.FromHours(2));

        var deadline = ResponseDeadline.For(ReasonCode.FraudCardAbsent, openedAtOtherOffset);

        Assert.Equal(new DateOnly(2026, 1, 31), deadline);
    }
}
