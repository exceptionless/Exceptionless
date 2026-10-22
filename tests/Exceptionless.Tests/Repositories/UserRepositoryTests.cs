using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Validation;
using Foundatio.Repositories;
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
    public async Task GetUserByOAuthProviderAsync_DifferentAccountEntries_RequiresExactIdentityPair()
    {
        // Arrange
        var user = new User { FullName = "Linked User", EmailAddress = "linked@localhost.test" };
        user.MarkEmailAddressVerified();
        user.AddOAuthAccount("google", "shared-subject", user.EmailAddress);
        user.AddOAuthAccount("google", "shared-subject", user.EmailAddress);
        user.AddOAuthAccount("github", "other-subject", user.EmailAddress);
        await _repository.AddAsync(user, options => options.ImmediateConsistency());

        // Act
        var githubUser = await _repository.GetUserByOAuthProviderAsync("github", "shared-subject", TestCancellationToken);
        var googleUser = await _repository.GetUserByOAuthProviderAsync("google", "shared-subject", TestCancellationToken);
        var differentProviderCaseUser = await _repository.GetUserByOAuthProviderAsync("GOOGLE", "shared-subject", TestCancellationToken);
        var differentCaseUser = await _repository.GetUserByOAuthProviderAsync("google", "SHARED-SUBJECT", TestCancellationToken);

        // Assert
        Assert.Null(githubUser);
        Assert.NotNull(googleUser);
        Assert.Equal(user.Id, googleUser.Id);
        Assert.Equal(2, googleUser.OAuthAccounts.Count);
        Assert.Null(differentCaseUser);
        Assert.Null(differentProviderCaseUser);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task GetUserByOAuthProviderAsync_PagedCandidates_ValidatesIdentityAndCancellation(bool hasDuplicateOwner, bool cancelAfterFirstPage)
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
        User? matchingUser = null;
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
            matchingUser = await _repository.GetUserByOAuthProviderAsync("github", "shared-subject", cancellation.Token);
        });

        // Assert
        if (cancelAfterFirstPage)
        {
            Assert.IsAssignableFrom<OperationCanceledException>(exception);
            Assert.Null(matchingUser);
            Assert.Equal(1, queryCount);
        }
        else if (hasDuplicateOwner)
        {
            Assert.Null(exception);
        }
        else
        {
            Assert.Null(exception);
            Assert.NotNull(matchingUser);
            Assert.Equal(users[^1].Id, matchingUser.Id);
        }
    }
}
