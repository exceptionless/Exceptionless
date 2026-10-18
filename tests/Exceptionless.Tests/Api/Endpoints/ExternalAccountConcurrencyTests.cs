using System.Net.Http.Json;
using System.Text;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Validation;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Models;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using User = Exceptionless.Core.Models.User;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class ExternalAccountConcurrencyTests : IntegrationTestsBase
{
    private readonly IUserRepository _users;
    private readonly ITokenRepository _tokens;
    private readonly ExternalAccountRace _race;

    public ExternalAccountConcurrencyTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _users = GetService<IUserRepository>();
        _tokens = GetService<ITokenRepository>();
        _race = GetService<ExternalAccountRace>();
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        base.RegisterServices(services);
        services.AddSingleton<ExternalAccountRace>();
        services.ReplaceSingleton<IUserRepository, PausingUserRepository>();
        services.ReplaceSingleton<ILockProvider>(provider =>
        {
            var inner = provider.GetRequiredService<CacheLockProvider>();
            var race = provider.GetRequiredService<ExternalAccountRace>();
            var locks = new Mock<ILockProvider>();
            locks.Setup(locker => locker.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(async (string resource, TimeSpan? expires, bool release, CancellationToken token) =>
                {
                    if (race.RejectLocks)
                        return null;

                    await race.ObserveLockAsync(inner, resource);
                    return await inner.TryAcquireAsync(resource, expires, release, token);
                });
            locks.Setup(locker => locker.AcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(async (string resource, TimeSpan? expires, bool release, CancellationToken token) =>
                {
                    await race.ObserveLockAsync(inner, resource);
                    return await inner.AcquireAsync(resource, expires, release, token);
                });
            return locks.Object;
        });
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        _race.Stop();
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
        var secondToken = await _tokens.GetByIdAsync(results[1]!.Token);
        Assert.NotNull(secondToken);
        Assert.Equal(owner.Id, secondToken.UserId);
    }

    [Theory]
    [InlineData(LookupStage.Identity)]
    [InlineData(LookupStage.Email)]
    [InlineData(LookupStage.User)]
    public async Task ExternalLogin_CanceledLookup_ReleasesLocksForNextRequest(LookupStage stage)
    {
        // Arrange
        const string code = "canceled-external-account";
        using var client = _server.CreateClient();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestCancellationToken);
        if (stage == LookupStage.User)
        {
            var user = new User { FullName = "Canceled lookup user", EmailAddress = TestOAuthProviderClient.GetEmailAddress(code) };
            user.MarkEmailAddressVerified();
            await _users.AddAsync(user, options => options.ImmediateConsistency());
        }
        _race.Start(stage, TestCancellationToken);
        var pendingRequest = client.PostAsJsonAsync("api/v2/auth/github", new ExternalAuthInfo
        {
            ClientId = "client-id", Code = code, RedirectUri = "http://localhost/callback"
        }, cancellation.Token);
        await _race.FirstSnapshotRead.Task.WaitAsync(TimeSpan.FromSeconds(10), TestCancellationToken);
        Exception? exception;
        TokenResult? nextResult;

        // Act
        try
        {
            cancellation.Cancel();
            exception = await Record.ExceptionAsync(async () =>
            {
                using var response = await pendingRequest;
            }).AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestCancellationToken);
            nextResult = await SendExternalLoginAsync(client, code);
        }
        finally
        {
            _race.ResumeFirstRequest.TrySetResult();
        }

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.NotNull(nextResult);
        Assert.NotNull(await _tokens.GetByIdAsync(nextResult.Token));
    }

    [Fact]
    public async Task ExternalLogin_AccountLockUnavailable_IsRejectedWithoutWrites()
    {
        // Arrange
        const string code = "external-account-lock-unavailable";
        using var client = _server.CreateClient();
        long originalUserCount = (await _users.CountAsync()).Total;
        long originalTokenCount = (await _tokens.CountAsync()).Total;
        _race.RejectLocks = true;

        // Act
        using var response = await client.PostAsJsonAsync("api/v2/auth/github", new ExternalAuthInfo
        {
            ClientId = "client-id",
            Code = code,
            RedirectUri = "http://localhost/callback"
        }, TestCancellationToken);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(originalUserCount, (await _users.CountAsync()).Total);
        Assert.Equal(originalTokenCount, (await _tokens.CountAsync()).Total);
        Assert.Null(await _users.GetUserByOAuthProviderAsync("github", code, TestCancellationToken));
    }

    [Fact]
    public async Task RemoveExternalLogin_AccountLockUnavailable_PreservesIdentityAndAuthenticationVersion()
    {
        // Arrange
        const string code = "external-unlink-lock-unavailable";
        using var client = _server.CreateClient();
        SetAuthorization(client, SampleDataService.TEST_ORG_USER_EMAIL, SampleDataService.TEST_ORG_USER_PASSWORD);
        var user = await _users.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);
        user.AddOAuthAccount("github", code, user.EmailAddress);
        await _users.SaveAsync(user, options => options.ImmediateConsistency());
        _race.RejectLocks = true;

        // Act
        using var response = await client.PostAsJsonAsync("api/v2/auth/unlink/github", new ValueFromBody<string>(code), TestCancellationToken);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        var storedUser = await _users.GetByIdAsync(user.Id, options => options.Cache(false));
        Assert.NotNull(storedUser);
        Assert.Equal(user.AuthenticationVersion, storedUser.AuthenticationVersion);
        Assert.Single(storedUser.OAuthAccounts, account => account.Provider == "github" && account.ProviderUserId == code);
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

    private async Task<TokenResult?[]> RunConcurrentRequestsAsync(HttpClient firstClient, string firstCode, HttpClient secondClient, string secondCode)
    {
        _race.Start(LookupStage.Identity, TestCancellationToken);
        var firstRequest = SendExternalLoginAsync(firstClient, firstCode);
        Task<TokenResult?>? secondRequest = null;
        try
        {
            await _race.FirstSnapshotRead.Task.WaitAsync(TimeSpan.FromSeconds(10), TestCancellationToken);
            secondRequest = SendExternalLoginAsync(secondClient, secondCode);
            await _race.SecondRequestObserved.Task.WaitAsync(TimeSpan.FromSeconds(10), TestCancellationToken);
        }
        finally
        {
            _race.ResumeFirstRequest.TrySetResult();
        }

        return await Task.WhenAll(firstRequest, secondRequest!);
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

    private sealed class PausingUserRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, Exceptionless.Core.AppOptions options, ExternalAccountRace race)
        : UserRepository(configuration, validator, options), IUserRepository
    {
        public new async Task<User?> GetUserByOAuthProviderAsync(string provider, string providerUserId, CancellationToken cancellationToken = default)
        {
            var user = await base.GetUserByOAuthProviderAsync(provider, providerUserId, cancellationToken);
            await race.AfterSnapshotReadAsync(LookupStage.Identity, cancellationToken);
            return user;
        }

        public new async Task<User?> GetByEmailAddressAsync(string emailAddress)
        {
            var user = await base.GetByEmailAddressAsync(emailAddress);
            await race.AfterSnapshotReadAsync(LookupStage.Email, race.TestCancellationToken);
            return user;
        }

        public override async Task<User?> GetByIdAsync(Id id, ICommandOptions? options = null)
        {
            var user = await base.GetByIdAsync(id, options);
            await race.AfterSnapshotReadAsync(LookupStage.User, race.TestCancellationToken);
            return user;
        }
    }

    public enum LookupStage { Identity, Email, User }

    private sealed class ExternalAccountRace
    {
        private int _reads;
        private bool Enabled { get; set; }
        private LookupStage Stage { get; set; }
        public CancellationToken TestCancellationToken { get; private set; }
        public bool RejectLocks { get; set; }
        public TaskCompletionSource FirstSnapshotRead { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondRequestObserved { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ResumeFirstRequest { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Start(LookupStage stage, CancellationToken testCancellationToken)
        {
            _reads = 0;
            Stage = stage;
            TestCancellationToken = testCancellationToken;
            FirstSnapshotRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
            SecondRequestObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
            ResumeFirstRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Enabled = true;
        }

        public void Stop()
        {
            Enabled = false;
            RejectLocks = false;
        }

        public async Task AfterSnapshotReadAsync(LookupStage stage, CancellationToken cancellationToken)
        {
            if (!Enabled || Stage != stage)
                return;

            if (Interlocked.Increment(ref _reads) == 1)
            {
                FirstSnapshotRead.TrySetResult();
                await ResumeFirstRequest.Task.WaitAsync(cancellationToken);
            }
            else
            {
                SecondRequestObserved.TrySetResult();
            }
        }

        public async Task ObserveLockAsync(ILockProvider provider, string resource)
        {
            if (Enabled && FirstSnapshotRead.Task.IsCompleted && await provider.IsLockedAsync(resource))
                SecondRequestObserved.TrySetResult();
        }
    }
}
