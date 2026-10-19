using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests.Extensions;

public sealed class UserExtensionsTests : TestWithLoggingBase
{
    public UserExtensionsTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData("unknown")]
    [InlineData("GitHub")]
    [InlineData("")]
    public void OAuthAccount_InvalidProvider_IsRejectedWithoutChangingLinks(string providerName)
    {
        // Arrange
        var user = new User { FullName = "Linked user", EmailAddress = "linked@localhost.test" };
        user.AddOAuthAccount("github", "existing-subject", user.EmailAddress);
        var originalAccount = user.OAuthAccounts.Single();

        // Act
        var addException = Record.Exception(() => user.AddOAuthAccount(providerName, "new-subject", user.EmailAddress));
        var removeException = Record.Exception(() => user.RemoveOAuthAccount(providerName, "existing-subject"));

        // Assert
        Assert.Equal("providerName", Assert.IsType<ArgumentException>(addException).ParamName);
        Assert.Equal("providerName", Assert.IsType<ArgumentException>(removeException).ParamName);
        Assert.Equal(originalAccount, Assert.Single(user.OAuthAccounts));
    }
}
