using System.Security.Cryptography;
using System.Text;
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

    [Fact]
    public void GetClaimId_UsesSecretAndCanonicalMailbox_WithoutRetainingUnkeyedDigest()
    {
        // Arrange
        string firstKey = Convert.ToBase64String(new byte[32]);
        string secondKey = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray());
        const string email = "Some.One+first@gmail.com";
        string legacyDigest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("someone@gmail.com")));

        // Act
        string first = TrialEmailAddress.GetClaimId(email, firstKey);
        string alias = TrialEmailAddress.GetClaimId("s.o.m.e.o.n.e+alias@googlemail.com", firstKey);
        string otherSecret = TrialEmailAddress.GetClaimId(email, secondKey);
        string migrated = TrialEmailAddress.ProtectLegacyClaimId(legacyDigest, firstKey);

        // Assert
        Assert.Equal("hmac-v1-65a0093454a49ce28b1071316083b2ca23f73e235b6b99a621b044b8ebcccea9", first);
        Assert.Equal(first, alias);
        Assert.Equal(first, migrated);
        Assert.NotEqual(first, otherSecret);
        Assert.DoesNotContain(legacyDigest, first);
    }

    [Theory]
    [InlineData("someone@mail.tm", true)]
    [InlineData("someone@sub.tmpmail.org", true)]
    [InlineData("someone@notmail.tm", false)]
    public void IsBlockedDomain_ExpandedDomains_RespectsDomainBoundary(string email, bool expected)
    {
        // Act
        bool blocked = TrialEmailAddress.IsBlockedDomain(email, TrialEmailAddress.DefaultBlockedDomains);

        // Assert
        Assert.Equal(expected, blocked);
    }
}
