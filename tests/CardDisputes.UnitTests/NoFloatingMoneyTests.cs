#pragma warning disable CA1707
using System.Reflection;
using CardDisputes.Domain;

namespace CardDisputes.UnitTests;

public class NoFloatingMoneyTests
{
    [Fact]
    public void No_Amount_Property_Uses_Floating_Point()
    {
        var assembly = typeof(Marker).Assembly;
        var offenders = new List<string>();

        foreach (var type in assembly.GetExportedTypes())
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (!property.Name.Contains("Amount", StringComparison.Ordinal))
                {
                    continue;
                }

                if (property.PropertyType == typeof(decimal)
                    || property.PropertyType == typeof(double)
                    || property.PropertyType == typeof(float))
                {
                    offenders.Add($"{type.FullName}.{property.Name}");
                }
            }
        }

        Assert.Empty(offenders);
    }
}
