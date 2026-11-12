using System.Net;
using System.Net.Http.Json;
using System.Text;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Models;
using Exceptionless.Tests.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Web.Models;
using FluentRest;
using Foundatio.Repositories;
using Foundatio.Caching;
using Foundatio.Repositories.Models;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class ExternalAccountConcurrencyTests : IntegrationTestsBase
{
    private readonly IUserRepository _users;
    private readonly ITokenRepository _tokens;

    public ExternalAccountConcurrencyTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _users = GetService<IUserRepository>();
        _tokens = GetService<ITokenRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var options = GetService<Exceptionless.Core.Configuration.AuthOptions>();
        options.EnableAccountCreation = true;
        options.GitHubId = "github-client-id";
        options.GitHubSecret = "github-client-secret";
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExternalLogin_ConcurrentRequestsForSameIdentity_KeepOneOwner(bool linkExistingUsers)
    {
        // Arrange
        const string code = "concurrent-external-account";
        using var firstClient = _server.CreateClient();
        using var secondClient = _server.CreateClient();
        if (linkExistingUsers)
        {
            SetAuthorization(firstClient, SampleDataService.TEST_USER_EMAIL, SampleDataService.TEST_USER_PASSWORD);
            SetAuthorization(secondClient, SampleDataService.TEST_ORG_USER_EMAIL, SampleDataService.TEST_ORG_USER_PASSWORD);
        }

        // Act
        var results = await RunConcurrentRequestsAsync(firstClient, code, secondClient, code);

        // Assert
        Assert.All(results, result => Assert.NotNull(result));
        var candidates = await _users.FindAsync(query => query.FieldEquals(user => user.OAuthAccounts.First().ProviderUserId, code));
        var owner = Assert.Single(candidates.Documents);
        Assert.Single(owner.OAuthAccounts, account => account.Provider == "github" && account.ProviderUserId == code);
        var tokens = await _tokens.GetByIdsAsync(results.Select(result => result!.Token).ToArray());
        Assert.Contains(tokens, token => token.UserId == owner.Id);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("email")]
    [InlineData("user")]
    public async Task ExternalLogin_CanceledLookup_ReleasesLocksForNextRequest(string stage)
    {
        // Arrange
        const string code = "canceled-external-account";
        using var client = _server.CreateClient();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        var users = Assert.IsType<UserRepository>(_users);
        if (stage == "user")
        {
            var user = new User { FullName = "Canceled lookup user", EmailAddress = TestOAuthProviderClient.GetEmailAddress(code) };
            user.MarkEmailAddressVerified();
            await users.AddAsync(user, options => options.ImmediateConsistency());
        }

        bool canceled = false;
        int queries = 0;
        using var subscription = users.BeforeQuery.AddHandler((_, _) =>
        {
            queries++;
            if (!canceled && ((stage == "identity" && queries == 1) || (stage == "email" && queries == 2)))
            {
                canceled = true;
                cancellation.Cancel();
            }

            return Task.CompletedTask;
        });

        using var getSubscription = users.BeforeGet.AddHandler((_, _) =>
        {
            if (!canceled && stage == "user")
            {
                canceled = true;
                cancellation.Cancel();
            }

            return Task.CompletedTask;
        });

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            using var response = await client.PostAsJsonAsync("api/v2/auth/github", new ExternalAuthInfo
            {
                ClientId = "client-id", Code = code, RedirectUri = "http://localhost/callback"
            }, cancellation.Token);
        });
        var nextResult = await SendExternalLoginAsync(client, code);

        // Assert
        Assert.True(canceled);
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.NotNull(nextResult);
        Assert.NotNull(await _tokens.GetByIdAsync(nextResult.Token));
    }

    [Fact]
    public async Task ExternalLogin_DestinationVersionConflict_PreservesPreviousOwnerAndConcurrentChanges()
    {
        // Arrange
        const string code = "external-account-transfer-conflict";
        const string newPassword = "ChangedDestination2$";
        var owner = await _users.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        var destination = await _users.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(owner);
        Assert.NotNull(destination);
        owner.AddOAuthAccount("github", code, "original-provider-name", new SettingsDictionary { ["profile"] = "original" });
        await _users.SaveAsync(owner);
        string? originalAuthenticationVersion = owner.AuthenticationVersion;
        bool destinationChanged = false;
        TokenResult? replacementToken = null;
        using var subscription = _users.DocumentsSaving.AddHandler(async (_, args) =>
        {
            if (destinationChanged || !args.Documents.Any(document => document.Value.Id == destination.Id))
                return;

            destinationChanged = true;
            replacementToken = await SendRequestAsAsync<TokenResult>(request => request.AsTestOrganizationUser()
                .Post().AppendPath("auth/change-password")
                .Content(new ChangePasswordModel { CurrentPassword = SampleDataService.TEST_ORG_USER_PASSWORD, Password = newPassword })
                .StatusCodeShouldBeOk());
            var currentOwner = await _users.GetByIdAsync(owner.Id, options => options.Cache(false))
                ?? throw new InvalidOperationException("The previous owner no longer exists.");
            currentOwner.FullName = "Concurrent owner update";
            await _users.SaveAsync(currentOwner);
        });
        using var client = _server.CreateClient();
        SetAuthorization(client, SampleDataService.TEST_ORG_USER_EMAIL, SampleDataService.TEST_ORG_USER_PASSWORD);

        // Act
        using var response = await client.PostAsJsonAsync("api/v2/auth/github", new ExternalAuthInfo
        {
            ClientId = "client-id", Code = code, RedirectUri = "http://localhost/callback"
        }, TestCancellationToken);
        var storedOwner = await _users.GetByIdAsync(owner.Id, options => options.Cache(false));
        var storedDestination = await _users.GetByIdAsync(destination.Id, options => options.Cache(false));
        var owners = await _users.GetUsersByOAuthProviderAsync("github", code, TestCancellationToken);

        // Assert
        Assert.True(destinationChanged);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(owner.Id, Assert.Single(owners).Id);
        Assert.NotNull(storedOwner);
        Assert.Equal("Concurrent owner update", storedOwner.FullName);
        Assert.NotEqual(originalAuthenticationVersion, storedOwner.AuthenticationVersion);
        var restoredAccount = Assert.Single(storedOwner.OAuthAccounts, account => account.Provider == "github" && account.ProviderUserId == code);
        Assert.Equal("original-provider-name", restoredAccount.Username);
        Assert.Equal("original", restoredAccount.ExtraData["profile"]);
        Assert.NotNull(storedDestination);
        Assert.True(storedDestination.IsCorrectPassword(newPassword));
        Assert.DoesNotContain(storedDestination.OAuthAccounts, account => account.Provider == "github" && account.ProviderUserId == code);
        Assert.NotNull(replacementToken);
        await SendRequestAsync(request => request.BearerToken(replacementToken.Token).AppendPath("users/me").StatusCodeShouldBeOk());
    }

    [Fact]
    public async Task ExternalLogin_StaleCachedEmailMiss_LinksStoredUserWithoutCreatingDuplicate()
    {
        // Arrange
        const string code = "external-account-stale-email-miss";
        var user = new User { FullName = "Existing email owner", EmailAddress = TestOAuthProviderClient.GetEmailAddress(code) };
        user.MarkEmailAddressVerified();
        await _users.AddAsync(user, options => options.ImmediateConsistency());
        var cache = new ScopedCacheClient(GetService<ExceptionlessElasticConfiguration>().Cache, nameof(User));
        await cache.SetAsync<ICollection<FindHit<User>>>($"Email:{user.EmailAddress}", []);
        var staleEmailLookup = await _users.GetByEmailAddressAsync(user.EmailAddress);
        long originalUserCount = (await _users.CountAsync()).Total;
        using var client = _server.CreateClient();

        // Act
        var result = await SendExternalLoginAsync(client, code);

        // Assert
        Assert.Null(staleEmailLookup);
        Assert.NotNull(result);
        var token = await _tokens.GetByIdAsync(result.Token);
        Assert.NotNull(token);
        Assert.Equal(user.Id, token.UserId);
        Assert.Equal(originalUserCount, (await _users.CountAsync()).Total);
        var storedUser = await _users.GetByIdAsync(user.Id, options => options.Cache(false));
        Assert.NotNull(storedUser);
        Assert.Single(storedUser.OAuthAccounts, account => account.Provider == "github" && account.ProviderUserId == code);
    }

    [Fact]
    public async Task ExternalLogin_IdentityUnlinkedAfterLookup_RejectsStaleOwner()
    {
        // Arrange
        const string code = "external-account-unlinked-during-login";
        var user = await _users.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);
        user.AddOAuthAccount("github", code, user.EmailAddress);
        await _users.SaveAsync(user, options => options.ImmediateConsistency());
        using var loginClient = _server.CreateClient();
        using var unlinkClient = _server.CreateClient();
        SetAuthorization(unlinkClient, SampleDataService.TEST_ORG_USER_EMAIL, SampleDataService.TEST_ORG_USER_PASSWORD);
        bool unlinked = false;
        HttpStatusCode? unlinkStatus = null;
        using var subscription = _users.AfterQuery.AddHandler(async (_, args) =>
        {
            if (unlinked || args.Result is not FindResults<User> result || !result.Documents.Any(candidate => candidate.Id == user.Id))
                return;

            unlinked = true;
            using var response = await unlinkClient.PostAsJsonAsync("api/v2/auth/unlink/github", new ValueFromBody<string>(code), TestCancellationToken);
            unlinkStatus = response.StatusCode;
        });

        // Act
        using var loginResponse = await loginClient.PostAsJsonAsync("api/v2/auth/github", new ExternalAuthInfo
        {
            ClientId = "client-id", Code = code, RedirectUri = "http://localhost/callback"
        }, TestCancellationToken);
        var storedUser = await _users.GetByIdAsync(user.Id, options => options.Cache(false));

        // Assert
        Assert.True(unlinked);
        Assert.Equal(HttpStatusCode.OK, unlinkStatus);
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
        Assert.NotNull(storedUser);
        Assert.DoesNotContain(storedUser.OAuthAccounts, account => account.Provider == "github" && account.ProviderUserId == code);
    }

    [Fact]
    public async Task ExternalLogin_ConcurrentDifferentIdentitiesForSameUser_PreservesBothLinks()
    {
        // Arrange
        using var firstClient = _server.CreateClient();
        using var secondClient = _server.CreateClient();
        SetAuthorization(firstClient, SampleDataService.TEST_ORG_USER_EMAIL, SampleDataService.TEST_ORG_USER_PASSWORD);
        SetAuthorization(secondClient, SampleDataService.TEST_ORG_USER_EMAIL, SampleDataService.TEST_ORG_USER_PASSWORD);
        var currentUser = await _users.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(currentUser);

        // Act
        var results = await RunConcurrentRequestsAsync(firstClient, "concurrent-first-account", secondClient, "concurrent-second-account");

        // Assert
        Assert.All(results, result => Assert.NotNull(result));
        var storedUser = await _users.GetByIdAsync(currentUser.Id, options => options.Cache(false));
        Assert.NotNull(storedUser);
        Assert.Single(storedUser.OAuthAccounts, account => account.Provider == "github" && account.ProviderUserId == "concurrent-first-account");
        Assert.Single(storedUser.OAuthAccounts, account => account.Provider == "github" && account.ProviderUserId == "concurrent-second-account");
    }

    private Task<TokenResult?[]> RunConcurrentRequestsAsync(HttpClient firstClient, string firstCode, HttpClient secondClient, string secondCode)
    {
        return Task.WhenAll(SendExternalLoginAsync(firstClient, firstCode), SendExternalLoginAsync(secondClient, secondCode));
    }

    private static void SetAuthorization(HttpClient client, string email, string password)
    {
        client.DefaultRequestHeaders.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{password}")));
    }

    private async Task<TokenResult?> SendExternalLoginAsync(HttpClient client, string code)
    {
        using var response = await client.PostAsJsonAsync("api/v2/auth/github", new ExternalAuthInfo
        {
            ClientId = "client-id",
            Code = code,
            RedirectUri = "http://localhost/callback"
        }, TestCancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TokenResult>(TestCancellationToken);
    }
}
