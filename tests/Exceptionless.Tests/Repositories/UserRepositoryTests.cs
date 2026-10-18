using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Repositories;

public sealed class UserRepositoryTests : IntegrationTestsBase
{
    private readonly IUserRepository _repository;

    public UserRepositoryTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _repository = GetService<IUserRepository>();
    }

    [Fact]
    public async Task GetUserByOAuthProviderAsync_DifferentAccountEntries_RequiresExactIdentityPair()
    {
        // Arrange
        var user = new User { FullName = "Linked User", EmailAddress = "linked@localhost.test" };
        user.MarkEmailAddressVerified();
        user.AddOAuthAccount("google", "shared-subject", user.EmailAddress);
        user.AddOAuthAccount("GOOGLE", "shared-subject", user.EmailAddress);
        user.AddOAuthAccount("github", "other-subject", user.EmailAddress);
        await _repository.AddAsync(user, options => options.ImmediateConsistency());

        // Act
        var githubUser = await _repository.GetUserByOAuthProviderAsync("github", "shared-subject");
        var googleUser = await _repository.GetUserByOAuthProviderAsync("GOOGLE", "shared-subject");
        var differentCaseUser = await _repository.GetUserByOAuthProviderAsync("google", "SHARED-SUBJECT");

        // Assert
        Assert.Null(githubUser);
        Assert.NotNull(googleUser);
        Assert.Equal(user.Id, googleUser.Id);
        Assert.Equal(2, googleUser.OAuthAccounts.Count);
        Assert.Null(differentCaseUser);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetUserByOAuthProviderAsync_MatchingIdentityOnSecondPage_ValidatesAllCandidates(bool hasDuplicateOwner)
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

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            matchingUser = await _repository.GetUserByOAuthProviderAsync("github", "shared-subject");
        });

        // Assert
        if (hasDuplicateOwner)
        {
            Assert.IsType<UnauthorizedAccessException>(exception);
            Assert.Null(matchingUser);
        }
        else
        {
            Assert.Null(exception);
            Assert.NotNull(matchingUser);
            Assert.Equal(users[^1].Id, matchingUser.Id);
        }
    }
}
