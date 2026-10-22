using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests.Extensions;

public sealed class UserExtensionsTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddOAuthAccount_ExistingIdentity_RefreshesMetadataWithoutAddingAccount(bool includeData)
    {
        // Arrange
        var user = new User();
        user.AddOAuthAccount("github", "subject", "old@example.test", new SettingsDictionary
        {
            ["name"] = "Old name", ["retained"] = "keep"
        });
        user.AddOAuthAccount("google", "subject", "other@example.test");
        var account = user.OAuthAccounts.First();
        var data = includeData ? new SettingsDictionary { ["name"] = "New name", ["added"] = "new" } : null;

        // Act
        user.AddOAuthAccount("github", "subject", "new@example.test", data);

        // Assert
        Assert.Equal(2, user.OAuthAccounts.Count);
        Assert.Same(account, user.OAuthAccounts.First());
        Assert.Equal("new@example.test", account.Username);
        Assert.Equal(includeData ? "New name" : "Old name", account.ExtraData["name"]);
        Assert.Equal("keep", account.ExtraData["retained"]);
        Assert.Equal(includeData, account.ExtraData.ContainsKey("added"));
        Assert.Equal("other@example.test", user.OAuthAccounts.Last().Username);
    }
}
