using Exceptionless.Core.Billing;
using Xunit;

namespace Exceptionless.Tests.Billing;

public class TrialEmailAddressTests
{
    [Theory]
    [InlineData("Someone@Example.com", "someone@example.com")]
    [InlineData("someone+trial@example.com", "someone@example.com")]
    [InlineData("some.one@example.com", "some.one@example.com")]
    [InlineData("Some.One+Second@Gmail.com", "someone@gmail.com")]
    [InlineData("s.o.m.e.o.n.e@googlemail.com", "someone@gmail.com")]
    [InlineData("+leading@example.com", "+leading@example.com")]
    [InlineData("  padded@example.com ", "padded@example.com")]
    public void Normalize_EmailAddress_RemovesIgnoredParts(string emailAddress, string expected)
    {
        // Act
        string normalized = TrialEmailAddress.Normalize(emailAddress);

        // Assert
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("someone@mailinator.com", true)]
    [InlineData("someone@inbox.mailinator.com", true)]
    [InlineData("someone@notmailinator.com", false)]
    [InlineData("someone@example.com", false)]
    public void IsBlockedDomain_DefaultDomains_MatchesDomainAndSubdomains(string emailAddress, bool expected)
    {
        // Act
        bool blocked = TrialEmailAddress.IsBlockedDomain(emailAddress, TrialEmailAddress.DefaultBlockedDomains);

        // Assert
        Assert.Equal(expected, blocked);
    }

    [Fact]
    public void NormalizeDomains_AdminInput_TrimsLowercasesDeduplicatesAndSorts()
    {
        // Act
        var domains = TrialEmailAddress.NormalizeDomains([" Example.COM ", "@example.com", "", "b.test", "a.test"]);

        // Assert
        Assert.Equal(["a.test", "b.test", "example.com"], domains);
    }
}
