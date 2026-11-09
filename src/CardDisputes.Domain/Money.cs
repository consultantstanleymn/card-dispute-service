namespace CardDisputes.Domain;

public readonly record struct Money
{
    public const long MinAmount = 1;
    public const long MaxAmount = 10_000_000;

    public Money(long amount, string currency)
    {
        if (amount is < MinAmount or > MaxAmount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Amount must be between 1 and 10,000,000 minor units.");
        }

        ArgumentException.ThrowIfNullOrEmpty(currency);
        if (currency.Length != 3
            || !IsUpperLetter(currency[0])
            || !IsUpperLetter(currency[1])
            || !IsUpperLetter(currency[2]))
        {
            throw new ArgumentException("Currency must be three uppercase letters.", nameof(currency));
        }

        Amount = amount;
        Currency = currency;
    }

    public long Amount { get; }

    public string Currency { get; }

    private static bool IsUpperLetter(char value) => value is >= 'A' and <= 'Z';
}
