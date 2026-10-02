using System.Collections;
using Exceptionless.Core.Configuration;
using Xunit;

namespace Exceptionless.Tests.Configuration;

public class CustomEnvironmentVariablesConfigurationProviderTests
{
    [Fact]
    public void Load_ExAndAspireVariablesNormalizeToSameKey_ExValueWinsRegardlessOfEnumerationOrder()
    {
        var first = new System.Collections.Specialized.OrderedDictionary
        {
            ["EX_ConnectionStrings__Redis"] = "ex:6379",
            ["ConnectionStrings__Redis"] = "aspire:6379"
        };
        var second = new System.Collections.Specialized.OrderedDictionary
        {
            ["ConnectionStrings__Redis"] = "aspire:6379",
            ["EX_ConnectionStrings__Redis"] = "ex:6379"
        };

        var firstProvider = new CustomEnvironmentVariablesConfigurationProvider();
        firstProvider.Load(first);
        var secondProvider = new CustomEnvironmentVariablesConfigurationProvider();
        secondProvider.Load(second);

        Assert.True(firstProvider.TryGet("ConnectionStrings:Redis", out string? firstValue));
        Assert.True(secondProvider.TryGet("ConnectionStrings:Redis", out string? secondValue));
        Assert.Equal("ex:6379", firstValue);
        Assert.Equal("ex:6379", secondValue);
    }
}
