namespace CardDisputes.UnitTests;

public class SmokeTests
{
    [Fact]
    public void DomainAssemblyHasExpectedName()
    {
        Assert.Equal("CardDisputes.Domain", typeof(CardDisputes.Domain.Marker).Assembly.GetName().Name);
    }
}
