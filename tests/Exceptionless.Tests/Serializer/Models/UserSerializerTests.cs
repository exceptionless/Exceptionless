using Exceptionless.Core.Models;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests User model serialization through ITextSerializer.
/// Critical: Validates that collection properties (OrganizationIds, OAuthAccounts, Roles)
/// survive round-trip serialization. This is essential because STJ cannot deserialize
/// into getter-only collection properties (e.g., `{ get; } = new()`).
/// </summary>
public class UserSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;
    private static readonly DateTime FixedDateTime = new(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    public UserSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Deserialize_User_PreservesOrganizationIds()
    {
        // Arrange - This is the critical test that would have caught the STJ bug
        var original = new User
        {
            Id = "user123",
            FullName = "Test User",
            EmailAddress = "test@example.com",
            IsEmailAddressVerified = true
        };
        original.OrganizationIds.Add("org1");
        original.OrganizationIds.Add("org2");
        original.OrganizationIds.Add("org3");

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<User>(json);

        // Assert - This would have failed with getter-only OrganizationIds
        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized.OrganizationIds.Count);
        Assert.Contains("org1", deserialized.OrganizationIds);
        Assert.Contains("org2", deserialized.OrganizationIds);
        Assert.Contains("org3", deserialized.OrganizationIds);
    }

    [Fact]
    public void Deserialize_User_PreservesRoles()
    {
        // Arrange
        var original = new User
        {
            Id = "user123",
            FullName = "Admin User",
            EmailAddress = "admin@example.com",
            IsEmailAddressVerified = true
        };
        original.Roles.Add("global");
        original.Roles.Add("user");
        original.Roles.Add("client");

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<User>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized.Roles.Count);
        Assert.Contains("global", deserialized.Roles);
        Assert.Contains("user", deserialized.Roles);
        Assert.Contains("client", deserialized.Roles);
    }

    [Fact]
    public void Deserialize_User_PreservesOAuthAccounts()
    {
        // Arrange
        var original = new User
        {
            Id = "user123",
            FullName = "OAuth User",
            EmailAddress = "oauth@example.com",
            IsEmailAddressVerified = true,
            OAuthAccounts =
            [
                new OAuthAccount
                {
                    Provider = "github",
                    ProviderUserId = "gh-12345",
                    Username = "testuser",
                    ExtraData = new SettingsDictionary
                    {
                        ["email"] = "oauth@example.com",
                        ["name"] = "OAuth User"
                    }
                },
                new OAuthAccount
                {
                    Provider = "google",
                    ProviderUserId = "google-67890",
                    Username = "oauth@example.com"
                }
            ]
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<User>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.OAuthAccounts.Count);

        var githubAccount = deserialized.OAuthAccounts.FirstOrDefault(a => a.Provider == "github");
        Assert.NotNull(githubAccount);
        Assert.Equal("gh-12345", githubAccount.ProviderUserId);
        Assert.Equal("testuser", githubAccount.Username);
        Assert.Equal(2, githubAccount.ExtraData.Count);
        Assert.Equal("oauth@example.com", githubAccount.ExtraData["email"]);
        Assert.Equal("OAuth User", githubAccount.ExtraData["name"]);

        var googleAccount = deserialized.OAuthAccounts.FirstOrDefault(a => a.Provider == "google");
        Assert.NotNull(googleAccount);
        Assert.Equal("google-67890", googleAccount.ProviderUserId);
    }

    [Fact]
    public void Deserialize_User_PreservesAllCollectionsTogether()
    {
        // Arrange - Tests all collections at once to ensure no interference
        var original = new User
        {
            Id = "complete-user",
            FullName = "Complete User",
            EmailAddress = "complete@example.com",
            IsEmailAddressVerified = true,
            OAuthAccounts =
            [
                new OAuthAccount
                {
                    Provider = "github",
                    ProviderUserId = "gh-12345",
                    Username = "completeuser"
                }
            ]
        };
        original.OrganizationIds.Add("org-a");
        original.OrganizationIds.Add("org-b");
        original.Roles.Add("global");
        original.Roles.Add("user");

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<User>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("complete-user", deserialized.Id);
        Assert.Equal("Complete User", deserialized.FullName);
        Assert.Equal("complete@example.com", deserialized.EmailAddress);

        // Verify all collections preserved
        Assert.Equal(2, deserialized.OrganizationIds.Count);
        Assert.Contains("org-a", deserialized.OrganizationIds);
        Assert.Contains("org-b", deserialized.OrganizationIds);

        Assert.Equal(2, deserialized.Roles.Count);
        Assert.Contains("global", deserialized.Roles);
        Assert.Contains("user", deserialized.Roles);

        Assert.Single(deserialized.OAuthAccounts);
        Assert.Equal("github", deserialized.OAuthAccounts.First().Provider);
    }

    [Fact]
    public void Deserialize_SnakeCaseJson_PreservesOrganizationIds()
    {
        // Arrange - Tests parsing snake_case JSON (cache deserialization scenario)
        // This is the critical test that validates cache deserialization works correctly.
        // The organization_ids and roles are the most important collections to preserve.
        /* language=json */
        const string json = """
            {
                "id": "user-from-cache",
                "full_name": "Cached User",
                "email_address": "cached@example.com",
                "is_email_address_verified": true,
                "organization_ids": ["cached-org-1", "cached-org-2"],
                "roles": ["user", "client"]
            }
            """;

        // Act
        var user = _serializer.Deserialize<User>(json);

        // Assert - Critical: organization_ids must be populated
        Assert.NotNull(user);
        Assert.Equal("user-from-cache", user.Id);
        Assert.Equal(2, user.OrganizationIds.Count);
        Assert.Contains("cached-org-1", user.OrganizationIds);
        Assert.Contains("cached-org-2", user.OrganizationIds);

        Assert.Equal(2, user.Roles.Count);
        Assert.Contains("user", user.Roles);
        Assert.Contains("client", user.Roles);
    }

    [Fact]
    public void Deserialize_SnakeCaseJson_PreservesOAuthAccounts()
    {
        // This test validates that OAuthAccounts survive a round-trip serialization
        // (the actual cache scenario) rather than parsing hand-crafted JSON.
        // Hand-crafted JSON may not match the exact format the serializer produces.
        
        // Arrange - Create user with OAuth account
        var original = new User
        {
            Id = "user-with-oauth",
            FullName = "OAuth User",
            EmailAddress = "oauth@example.com",
            IsEmailAddressVerified = true,
            OAuthAccounts =
            [
                new OAuthAccount
                {
                    Provider = "github",
                    ProviderUserId = "gh-cache",
                    Username = "cacheduser",
                    ExtraData = new SettingsDictionary { ["email"] = "oauth@example.com" }
                }
            ]
        };
        original.OrganizationIds.Add("org1");

        // Act - Round-trip through serializer (simulates cache save/load)
        string? json = _serializer.SerializeToString(original);
        var user = _serializer.Deserialize<User>(json);

        // Assert
        Assert.NotNull(user);
        Assert.Single(user.OrganizationIds);
        Assert.Single(user.OAuthAccounts);
        Assert.Equal("github", user.OAuthAccounts.First().Provider);
        Assert.Equal("gh-cache", user.OAuthAccounts.First().ProviderUserId);
        Assert.Equal("cacheduser", user.OAuthAccounts.First().Username);
        Assert.Equal("oauth@example.com", user.OAuthAccounts.First().ExtraData["email"]);
    }

    [Fact]
    public void Deserialize_UserWithEmptyCollections_ReturnsEmptyCollections()
    {
        // Arrange
        var original = new User
        {
            Id = "empty-user",
            FullName = "Empty Collections User",
            EmailAddress = "empty@example.com",
            IsEmailAddressVerified = true
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<User>(json);

        // Assert - Empty collections should still be empty, not null
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.OrganizationIds);
        Assert.Empty(deserialized.OrganizationIds);
        Assert.NotNull(deserialized.Roles);
        Assert.Empty(deserialized.Roles);
        Assert.NotNull(deserialized.OAuthAccounts);
        Assert.Empty(deserialized.OAuthAccounts);
    }

    [Fact]
    public void Deserialize_UserWithAllProperties_PreservesAllValues()
    {
        // Arrange
        var original = new User
        {
            Id = "full-user",
            FullName = "Full Test User",
            EmailAddress = "full@example.com",
            Password = "hashedpassword",
            Salt = "randomsalt",
            PasswordResetToken = "reset-token-123",
            PasswordResetTokenExpiration = FixedDateTime.AddDays(1),
            IsEmailAddressVerified = true,
            EmailNotificationsEnabled = false,
            IsActive = true,
            CreatedUtc = FixedDateTime.AddDays(-30),
            UpdatedUtc = FixedDateTime
        };
        original.OrganizationIds.Add("primary-org");
        original.Roles.Add("user");

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<User>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("full-user", deserialized.Id);
        Assert.Equal("Full Test User", deserialized.FullName);
        Assert.Equal("full@example.com", deserialized.EmailAddress);
        Assert.Equal("hashedpassword", deserialized.Password);
        Assert.Equal("randomsalt", deserialized.Salt);
        Assert.Equal("reset-token-123", deserialized.PasswordResetToken);
        Assert.True(deserialized.IsEmailAddressVerified);
        Assert.False(deserialized.EmailNotificationsEnabled);
        Assert.True(deserialized.IsActive);
        Assert.Single(deserialized.OrganizationIds);
        Assert.Single(deserialized.Roles);
    }

    [Fact]
    public void MultipleDeserializations_PreservesDataIntegrity()
    {
        // Arrange - Simulates multiple cache read/write cycles
        var original = new User
        {
            Id = "multi-cycle-user",
            FullName = "Multi Cycle User",
            EmailAddress = "multi@example.com",
            IsEmailAddressVerified = true
        };
        original.OrganizationIds.Add("org1");
        original.OrganizationIds.Add("org2");
        original.Roles.Add("admin");

        // Act - Serialize/deserialize multiple times
        var current = original;
        for (int i = 0; i < 5; i++)
        {
            string? json = _serializer.SerializeToString(current);
            current = _serializer.Deserialize<User>(json)!;
        }

        // Assert - Data should survive multiple cycles
        Assert.NotNull(current);
        Assert.Equal("multi-cycle-user", current.Id);
        Assert.Equal(2, current.OrganizationIds.Count);
        Assert.Contains("org1", current.OrganizationIds);
        Assert.Contains("org2", current.OrganizationIds);
        Assert.Single(current.Roles);
        Assert.Contains("admin", current.Roles);
    }
}
