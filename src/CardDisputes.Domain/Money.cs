namespace CardDisputes.Domain;

public sealed class Money
{
    public const long MaxAmount = 10_000_000;

    public Money(long amount, string currency) => throw new NotImplementedException();

    public long Amount => throw new NotImplementedException();

    public string Currency => throw new NotImplementedException();
}
