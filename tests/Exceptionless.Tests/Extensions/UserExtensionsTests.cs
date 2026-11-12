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
        user.OAuthAccounts.Add(new OAuthAccount
        {
            Provider = "github", ProviderUserId = "subject", Username = "stale-duplicate@example.test",
            ExtraData = new SettingsDictionary { ["email"] = "stale-duplicate@example.test" }
        });
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
        Assert.Equal("other@example.test", Assert.Single(user.OAuthAccounts, account => account.Provider == "google").Username);
        Assert.DoesNotContain(user.OAuthAccounts, account => account.EmailAddress() == "stale-duplicate@example.test");
    }

    [Theory]
    [InlineData("facebook", false)]
    [InlineData("Facebook", false)]
    [InlineData("windowslive", false)]
    [InlineData("WindowsLive", false)]
    [InlineData("facebook", true)]
    [InlineData("windowslive", true)]
    public void RemoveOAuthAccount_OnlyRetiredProviderWouldRemain_PreservesActiveLogin(string retiredProvider, bool hasDuplicateIdentity)
    {
        // Arrange
        var user = new User();
        user.AddOAuthAccount("google", "active-user", "user@example.com");
        if (hasDuplicateIdentity)
            user.OAuthAccounts.Add(user.OAuthAccounts.First());
        user.OAuthAccounts.Add(new OAuthAccount { Provider = retiredProvider, ProviderUserId = "retired-user", Username = "user@example.com" });

        // Act
        bool removed = user.RemoveOAuthAccount("google", "active-user");

        // Assert
        Assert.False(removed);
        Assert.Equal(hasDuplicateIdentity ? 3 : 2, user.OAuthAccounts.Count);
        Assert.Contains(user.OAuthAccounts, account => account.ProviderUserId == "active-user");
    }

    [Theory]
    [InlineData("facebook")]
    [InlineData("windowslive")]
    public void RemoveOAuthAccount_OnlyRetiredLogin_RemovesAccount(string provider)
    {
        // Arrange
        var user = new User();
        user.AddOAuthAccount(provider, "retired-user", "user@example.com");

        // Act
        bool removed = user.RemoveOAuthAccount(provider, "retired-user");

        // Assert
        Assert.True(removed);
        Assert.Empty(user.OAuthAccounts);
    }

    [Theory]
    [InlineData(null, false, 1)]
    [InlineData("password-hash", true, 0)]
    public void RemoveOAuthAccount_LastActiveLogin_RequiresPassword(string? password, bool expectedRemoved, int expectedRemainingAccounts)
    {
        // Arrange
        var user = new User { Password = password };
        user.AddOAuthAccount("microsoft", "active-user", "user@example.com");

        // Act
        bool removed = user.RemoveOAuthAccount("microsoft", "active-user");

        // Assert
        Assert.Equal(expectedRemoved, removed);
        Assert.Equal(expectedRemainingAccounts, user.OAuthAccounts.Count);
    }

    [Fact]
    public void RemoveOAuthAccount_AnotherActiveLoginRemains_RemovesAccount()
    {
        // Arrange
        var user = new User();
        user.AddOAuthAccount("google", "google-user", "user@example.com");
        user.AddOAuthAccount("microsoft", "microsoft-user", "user@example.com");

        // Act
        bool removed = user.RemoveOAuthAccount("google", "google-user");

        // Assert
        Assert.True(removed);
        Assert.Equal("microsoft", Assert.Single(user.OAuthAccounts).Provider);
    }

    [Fact]
    public void RemoveOAuthAccount_AccountDoesNotExist_PreservesLogin()
    {
        // Arrange
        var user = new User();
        user.AddOAuthAccount("google", "active-user", "user@example.com");

        // Act
        bool removed = user.RemoveOAuthAccount("google", "missing-user");

        // Assert
        Assert.True(removed);
        Assert.Equal("active-user", Assert.Single(user.OAuthAccounts).ProviderUserId);
    }
}
