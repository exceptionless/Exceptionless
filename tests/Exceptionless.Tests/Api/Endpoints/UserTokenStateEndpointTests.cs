using System.Reflection;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Validation;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using Exceptionless.Web.Models.Admin;
using FluentRest;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class UserTokenStateEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : IntegrationTestsBase(output, factory)
{
    protected override void RegisterServices(IServiceCollection services)
    {
        base.RegisterServices(services);
        services.ReplaceSingleton<ITokenRepository, CachingTokenRepository>();
        services.ReplaceSingleton<IOAuthTokenRepository, CachingOAuthTokenRepository>();
        services.ReplaceSingleton<IOAuthApplicationRepository>(provider =>
        {
            var repository = new OAuthApplicationRepository(provider.GetRequiredService<ExceptionlessElasticConfiguration>(),
                provider.GetRequiredService<MiniValidationValidator>(), provider.GetRequiredService<AppOptions>());
            var proxy = DispatchProxy.Create<IOAuthApplicationRepository, PausingApplicationRepository>();
            ((PausingApplicationRepository)(object)proxy).Repository = repository;
            return proxy;
        });
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthenticateAsync_ChangedUserTokenWithLateCacheWrite_ReturnsUnauthorized(bool logout)
    {
        var repository = (CachingTokenRepository)GetService<ITokenRepository>();
        var login = await SendRequestAsAsync<TokenResult>(r => r.Post().AppendPath("auth/login")
            .Content(new Login { Email = SampleDataService.TEST_USER_EMAIL, Password = SampleDataService.TEST_USER_PASSWORD })
            .StatusCodeShouldBeOk());
        Assert.NotNull(login);
        if (!logout)
        {
            var access = await repository.GetByIdAsync(login.Token, o => o.Cache(false));
            Assert.NotNull(access);
            access.Type = TokenType.Access;
            await repository.SaveAsync(access, o => o.ImmediateConsistency());
        }
        await SendRequestAsync(r => r.BearerToken(login.Token).AppendPath("users/me").StatusCodeShouldBeOk());
        var stale = await repository.GetByIdAsync(login.Token, o => o.Cache(false));
        Assert.NotNull(stale);
        if (logout)
        {
            await SendRequestAsync(r => r.BearerToken(login.Token).AppendPath("auth/logout").StatusCodeShouldBeOk());
            Assert.Null(await repository.GetByIdAsync(login.Token, o => o.Cache(false)));
        }
        else
        {
            var current = await repository.GetByIdAsync(login.Token, o => o.Cache(false));
            Assert.NotNull(current);
            current.IsDisabled = true;
            await repository.SaveAsync(current, o => o.ImmediateConsistency());
        }
        await repository.CacheTokenAsync(stale);
        await SendRequestAsync(r => r.BearerToken(login.Token).AppendPath("users/me").StatusCodeShouldBeUnauthorized());
        await SendRequestAsync(r => r.BasicAuthorization("client", login.Token).AppendPath("users/me").StatusCodeShouldBeUnauthorized());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthenticateAsync_ChangedOAuthStateWithLateCacheWrite_ReturnsUnauthorized(bool disableApplication)
    {
        var applications = GetService<IOAuthApplicationRepository>();
        var tokens = (CachingOAuthTokenRepository)GetService<IOAuthTokenRepository>();
        var user = await GetService<IUserRepository>().GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(user);
        var now = TimeProvider.GetUtcNow().UtcDateTime;
        var application = new OAuthApplication
        {
            Id = ObjectId.GenerateNewId().ToString(), ClientId = "late-cache-client", Name = "Late cache client",
            RedirectUris = ["http://localhost/callback"], Scopes = [AuthorizationRoles.ProjectsRead],
            CreatedByUserId = user.Id, UpdatedByUserId = user.Id, CreatedUtc = now, UpdatedUtc = now
        };
        await applications.AddAsync(application, o => o.ImmediateConsistency());
        string accessToken = StringExtensions.GetRandomString(OAuthService.OAuthTokenLength);
        var stale = new OAuthToken
        {
            Id = ObjectId.GenerateNewId().ToString(), UserId = user.Id, AuthenticationVersion = user.AuthenticationVersion,
            ClientId = application.ClientId, GrantId = "late-cache-grant", Resource = "http://localhost:7110/api/v2",
            AccessTokenHash = OAuthService.CreateTokenHash(accessToken), Scopes = [AuthorizationRoles.ProjectsRead],
            OrganizationIds = [SampleDataService.TEST_ORG_ID], ExpiresUtc = now.AddHours(1),
            CreatedBy = user.Id, CreatedUtc = now, UpdatedUtc = now
        };
        await tokens.AddAsync(stale, o => o.ImmediateConsistency());
        await SendRequestAsync(r => r.BearerToken(accessToken).AppendPath("projects").StatusCodeShouldBeOk());

        if (disableApplication)
        {
            var service = GetService<OAuthService>();
            await service.ClearAccessTokenClientValidityCacheAsync(application.ClientId);
            var pause = ((PausingApplicationRepository)(object)applications).PauseNextRead();
            var validation = service.IsAccessTokenClientValidAsync(application.ClientId);
            await pause.Read.Task.WaitAsync(TimeSpan.FromSeconds(10), TestCancellationToken);
            try
            {
                await SendRequestAsync(r => r.Put().AsGlobalAdminUser().AppendPaths("admin", "oauth-applications", application.Id)
                    .Content(new UpdateOAuthApplication
                    {
                        ClientId = application.ClientId, Name = application.Name, RedirectUris = application.RedirectUris,
                        Scopes = application.Scopes, IsDisabled = true
                    }).StatusCodeShouldBeOk());
            }
            finally
            {
                pause.Resume.TrySetResult();
            }
            await validation;
        }
        else
        {
            // Revoke through the endpoint after the access-token lookup was warmed.
            await SendRequestAsync(r => r.Post().AppendPath("oauth/revoke")
                .Content(new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["token"] = accessToken, ["client_id"] = application.ClientId
                })).StatusCodeShouldBeOk());
            var stored = await tokens.GetByIdAsync(stale.Id, o => o.Cache(false));
            Assert.NotNull(stored);
            Assert.True(stored.IsDisabled);
            await tokens.CacheTokenAsync(stale);
        }
        await SendRequestAsync(r => r.BearerToken(accessToken).AppendPath("projects").StatusCodeShouldBeUnauthorized());
    }

    private sealed class CachingTokenRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options)
        : TokenRepository(configuration, validator, options)
    {
        public Task CacheTokenAsync(Token token) => AddDocumentsToCacheAsync(token, new CommandOptions<Token>().Cache(), false);
    }

    private sealed class CachingOAuthTokenRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options)
        : OAuthTokenRepository(configuration, validator, options)
    {
        public Task CacheTokenAsync(OAuthToken token) => AddDocumentsToCacheAsync(token, new CommandOptions<OAuthToken>().Cache(), false);
    }

    public class PausingApplicationRepository : DispatchProxy
    {
        public IOAuthApplicationRepository Repository { get; set; } = null!;
        private ReadPause? _pause;
        public ReadPause PauseNextRead() => _pause = new ReadPause();
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var result = method!.Invoke(Repository, args);
            if (method.Name == nameof(IOAuthApplicationRepository.GetByClientIdAsync) && Interlocked.Exchange(ref _pause, null) is { } pause)
                return WaitForReadAsync((Task<OAuthApplication?>)result!, pause);
            return result;
        }

        private static async Task<OAuthApplication?> WaitForReadAsync(Task<OAuthApplication?> read, ReadPause pause)
        {
            var application = await read;
            pause.Read.TrySetResult();
            await pause.Resume.Task;
            return application;
        }
    }

    public sealed class ReadPause
    {
        public TaskCompletionSource Read { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
