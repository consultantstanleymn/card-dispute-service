#pragma warning disable CA1707
using CardDisputes.Domain;

namespace CardDisputes.UnitTests;

public class MoneyTests
{
    [Fact]
    public void Amount_Below_1_Rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(0, "USD"));
    }

    [Fact]
    public void Amount_Above_Max_Rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(Money.MaxAmount + 1, "USD"));
    }

    [Theory]
    [InlineData("usd")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("U$D")]
    [InlineData("123")]
    [InlineData("")]
    public void Currency_Must_Be_Three_Upper_Letters(string currency)
    {
        Assert.Throws<ArgumentException>(() => new Money(100, currency));
    }
}
