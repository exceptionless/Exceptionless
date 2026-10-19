using System.Text.Json;
using System.Text.Json.Nodes;
using Elastic.Clients.Elasticsearch;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Validation;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch;
using Foundatio.Repositories.Exceptions;
using Foundatio.Repositories.Models;
using Xunit;

namespace Exceptionless.Tests.Repositories;

public sealed class UserRepositoryTests : IntegrationTestsBase
{
    private readonly IUserRepository _repository;

    public UserRepositoryTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _repository = GetService<IUserRepository>();
    }

    [Theory]
    [InlineData("id")]
    [InlineData("ids")]
    [InlineData("email")]
    public async Task GetAsync_StoredUserWithoutVersion_ReadsVersionWithoutRewritingDocument(string lookup)
    {
        // Arrange
        const string userId = "770000000000000000000096";
        const string emailAddress = "legacy@localhost.test";
        var configuration = GetService<ExceptionlessElasticConfiguration>();
        var source = new JsonObject
        {
            ["id"] = userId,
            ["email_address"] = emailAddress,
            ["full_name"] = "Existing User"
        };
        var indexed = await configuration.Client.IndexAsync(source,
            request => request.Index(configuration.Users.VersionedName).Id(userId).Refresh(Refresh.WaitFor),
            TestCancellationToken);
        string expectedVersion = new ElasticDocumentVersion(indexed.PrimaryTerm.GetValueOrDefault(), indexed.SeqNo.GetValueOrDefault());

        // Act
        var loaded = lookup switch
        {
            "ids" => (await _repository.GetByIdsAsync(new[] { userId })).Single(),
            "email" => await _repository.GetByEmailAddressAsync(emailAddress),
            _ => await _repository.GetByIdAsync(userId)
        };
        var stored = await configuration.Client.GetAsync<JsonElement>(
            configuration.Users.VersionedName, userId, TestCancellationToken);

        // Assert
        Assert.True(indexed.IsValidResponse);
        Assert.NotNull(loaded);
        Assert.Equal(expectedVersion, loaded.Version);
        Assert.Equal("Existing User", loaded.FullName);
        Assert.True(stored.IsValidResponse);
        Assert.Equal(indexed.SeqNo, stored.SeqNo);
        Assert.False(stored.Source.TryGetProperty("version", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PatchAsync_MembershipChanges_RefreshesCachedUserLookups(bool remove)
    {
        // Arrange
        const string organizationId = "770000000000000000000001";
        var user = new User { FullName = "Cached User", EmailAddress = "cached@localhost.test" };
        user.MarkEmailAddressVerified();
        if (remove)
            user.OrganizationIds.Add(organizationId);
        await _repository.AddAsync(user, options => options.Cache());
        var before = await _repository.GetByIdAsync(user.Id, options => options.Cache());
        var beforeEmail = await _repository.GetByEmailAddressAsync(user.EmailAddress);

        // Act
        bool patched = await _repository.PatchAsync(user.Id, new ActionPatch<User>(current =>
        {
            if (remove)
                current.OrganizationIds.Remove(organizationId);
            else
                current.OrganizationIds.Add(organizationId);
        }), options => options.Cache());
        var byId = await _repository.GetByIdAsync(user.Id, options => options.Cache());
        var byEmail = await _repository.GetByEmailAddressAsync(user.EmailAddress);
        var stored = await _repository.GetByIdAsync(user.Id, options => options.Cache(false));

        // Assert
        Assert.NotNull(before);
        Assert.NotNull(beforeEmail);
        Assert.Equal(remove, before.OrganizationIds.Contains(organizationId));
        Assert.Equal(remove, beforeEmail.OrganizationIds.Contains(organizationId));
        Assert.True(patched);
        Assert.NotNull(stored);
        Assert.NotEqual(before.Version, stored.Version);
        foreach (var loaded in new[] { byId, byEmail })
        {
            Assert.NotNull(loaded);
            Assert.Equal(!remove, loaded.OrganizationIds.Contains(organizationId));
            Assert.Equal(stored.Version, loaded.Version);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateProfileAsync_TourRecordedAfterSnapshot_PreservesProgressAndUnchangedFields(bool updateFullName)
    {
        // Arrange
        var user = new User { FullName = "Existing User", EmailAddress = "profile@localhost.test" };
        user.MarkEmailAddressVerified();
        await _repository.AddAsync(user, options => options.Cache());
        var recordedUtc = TimeProvider.GetUtcNow().UtcDateTime;
        await _repository.RecordProductTourAsync(user, "app_overview", recordedUtc);
        await _repository.GetByIdAsync(user.Id, options => options.Cache());
        await _repository.GetByEmailAddressAsync(user.EmailAddress);

        // Act
        var updated = await _repository.UpdateProfileAsync(user, updateFullName ? "Updated Name" : null, updateFullName ? null : false);
        var byId = await _repository.GetByIdAsync(user.Id, options => options.Cache());
        var byEmail = await _repository.GetByEmailAddressAsync(user.EmailAddress);

        // Assert
        Assert.Empty(user.ProductTours);
        foreach (var result in new[] { updated, byId, byEmail })
        {
            Assert.NotNull(result);
            Assert.Equal(recordedUtc, result.ProductTours["app_overview"].GetDateTime());
            Assert.Equal(updateFullName ? "Updated Name" : user.FullName, result.FullName);
            Assert.Equal(updateFullName && user.EmailNotificationsEnabled, result.EmailNotificationsEnabled);
            Assert.Equal(user.EmailAddress, result.EmailAddress);
            Assert.Equal(user.CreatedUtc, result.CreatedUtc);
            Assert.Equal(user.Roles, result.Roles);
        }
    }

    [Fact]
    public async Task UpdateProfileAsync_UserDeletedAfterSnapshot_DoesNotRecreateUser()
    {
        // Arrange
        var user = new User { FullName = "Deleted User", EmailAddress = "deleted@localhost.test" };
        user.MarkEmailAddressVerified();
        await _repository.AddAsync(user, options => options.Cache());
        await _repository.RemoveAsync(user);

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            _ = await _repository.UpdateProfileAsync(user, "Updated Name", null);
        });
        var stored = await _repository.GetByIdAsync(user.Id);

        // Assert
        Assert.IsType<DocumentNotFoundException>(failure);
        Assert.Null(stored);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveAsync_VersionChecks_RejectStaleOrMissingVersion(bool missingVersion)
    {
        // Arrange
        var user = new User { FullName = "Versioned User", EmailAddress = "versioned@localhost.test" };
        user.MarkEmailAddressVerified();
        await _repository.AddAsync(user, options => options.ImmediateConsistency());
        var stale = user with { FullName = "Stale profile" };
        user.AuthenticationVersion = "rotated-generation";
        string originalVersion = Assert.IsType<string>(user.Version);
        var saved = await _repository.SaveAsync(user, options => options.ImmediateConsistency());
        if (missingVersion)
            stale.Version = null!;

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            _ = await _repository.SaveAsync(stale);
        });
        var persisted = await _repository.GetByIdAsync(user.Id);

        // Assert
        Assert.IsAssignableFrom<DocumentException>(failure);
        if (!missingVersion)
            Assert.IsType<VersionConflictDocumentException>(failure);
        Assert.Same(user, saved);
        Assert.NotEqual(originalVersion, user.Version);
        Assert.NotNull(persisted);
        Assert.Equal("rotated-generation", persisted.AuthenticationVersion);
        Assert.Equal(user.FullName, persisted.FullName);
    }

    [Theory]
    [InlineData("unknown", "new-subject")]
    [InlineData("github", " ")]
    public async Task SaveAsync_InvalidOAuthIdentity_RejectsChangesBeforePersistence(string provider, string providerUserId)
    {
        // Arrange
        var user = new User { FullName = "Linked user", EmailAddress = "linked@localhost.test" };
        user.MarkEmailAddressVerified();
        user.AddOAuthAccount("github", "existing-subject", user.EmailAddress);
        await _repository.AddAsync(user, options => options.ImmediateConsistency());
        user.AddOAuthAccount(provider, providerUserId, user.EmailAddress);

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            _ = await _repository.SaveAsync(user, options => options.ImmediateConsistency());
        });

        // Assert
        Assert.IsType<MiniValidatorException>(exception);
        var storedUser = await _repository.GetByIdAsync(user.Id, options => options.Cache(false));
        Assert.NotNull(storedUser);
        var account = Assert.Single(storedUser.OAuthAccounts);
        Assert.Equal("github", account.Provider);
        Assert.Equal("existing-subject", account.ProviderUserId);
    }

    [Fact]
    public async Task GetUsersByOAuthProviderAsync_DifferentAccountEntries_RequiresExactIdentityPair()
    {
        // Arrange
        var user = new User { FullName = "Linked User", EmailAddress = "linked@localhost.test" };
        user.MarkEmailAddressVerified();
        user.AddOAuthAccount("google", "shared-subject", user.EmailAddress);
        user.AddOAuthAccount("google", "shared-subject", user.EmailAddress);
        user.AddOAuthAccount("github", "other-subject", user.EmailAddress);
        await _repository.AddAsync(user, options => options.ImmediateConsistency());

        // Act
        var githubUser = await _repository.GetUsersByOAuthProviderAsync("github", "shared-subject", TestCancellationToken);
        var googleUser = await _repository.GetUsersByOAuthProviderAsync("google", "shared-subject", TestCancellationToken);
        var differentProviderCaseUser = await _repository.GetUsersByOAuthProviderAsync("GOOGLE", "shared-subject", TestCancellationToken);
        var differentCaseUser = await _repository.GetUsersByOAuthProviderAsync("google", "SHARED-SUBJECT", TestCancellationToken);

        // Assert
        Assert.Empty(githubUser);
        var owner = Assert.Single(googleUser);
        Assert.Equal(user.Id, owner.Id);
        Assert.Equal(2, owner.OAuthAccounts.Count);
        Assert.Empty(differentCaseUser);
        Assert.Empty(differentProviderCaseUser);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task GetUsersByOAuthProviderAsync_PagedCandidates_ValidatesIdentityAndCancellation(bool hasDuplicateOwner, bool cancelAfterFirstPage)
    {
        // Arrange
        var users = Enumerable.Range(1, 101).Select(index =>
        {
            var user = new User
            {
                Id = index.ToString("x24"),
                FullName = $"Linked User {index}",
                EmailAddress = $"linked-{index}@localhost.test"
            };
            user.MarkEmailAddressVerified();
            user.AddOAuthAccount("google", "shared-subject", user.EmailAddress);
            user.AddOAuthAccount("github", "other-subject", user.EmailAddress);
            return user;
        }).ToArray();
        users[^1].AddOAuthAccount("github", "shared-subject", users[^1].EmailAddress);
        if (hasDuplicateOwner)
            users[0].AddOAuthAccount("github", "shared-subject", users[0].EmailAddress);
        await _repository.AddAsync(users, options => options.ImmediateConsistency());
        IReadOnlyCollection<User>? matchingUsers = null;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        int queryCount = 0;
        using var queryHandler = _repository.AfterQuery.AddHandler((_, args) =>
        {
            if (args.Result is FindResults<User>)
            {
                queryCount++;
                if (cancelAfterFirstPage)
                    cancellation.Cancel();
            }

            return Task.CompletedTask;
        });

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            matchingUsers = await _repository.GetUsersByOAuthProviderAsync("github", "shared-subject", cancellation.Token);
        });

        // Assert
        if (cancelAfterFirstPage)
        {
            Assert.IsAssignableFrom<OperationCanceledException>(exception);
            Assert.Null(matchingUsers);
            Assert.Equal(1, queryCount);
        }
        else
        {
            Assert.Null(exception);
            Assert.NotNull(matchingUsers);
            Assert.Equal(hasDuplicateOwner ? 2 : 1, matchingUsers.Count);
            Assert.Contains(matchingUsers, user => user.Id == users[^1].Id);
        }
    }
}
