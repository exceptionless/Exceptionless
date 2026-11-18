using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests.Extensions;

public sealed class UserExtensionsTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    [Theory]
    [InlineData("facebook")]
    [InlineData("Facebook")]
    [InlineData("windowslive")]
    [InlineData("WindowsLive")]
    public void RemoveOAuthAccount_OnlyRetiredProviderWouldRemain_PreservesActiveLogin(string retiredProvider)
    {
        // Arrange
        var user = new User();
        user.AddOAuthAccount("google", "active-user", "user@example.com");
        user.OAuthAccounts.Add(new OAuthAccount { Provider = retiredProvider, ProviderUserId = "retired-user", Username = "user@example.com" });

        // Act
        bool removed = user.RemoveOAuthAccount("google", "active-user");

        // Assert
        Assert.False(removed);
        Assert.Equal(2, user.OAuthAccounts.Count);
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
