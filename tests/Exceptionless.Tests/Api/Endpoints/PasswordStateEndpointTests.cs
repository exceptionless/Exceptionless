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
using FluentRest;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class PasswordStateEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : IntegrationTestsBase(output, factory)
{
    protected override void RegisterServices(IServiceCollection services)
    {
        base.RegisterServices(services);
        services.ReplaceSingleton<IUserRepository, CachingUserRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Theory]
    [InlineData("password", true)]
    [InlineData("password", false)]
    [InlineData("inactive", true)]
    [InlineData("inactive", false)]
    [InlineData("email", true)]
    [InlineData("email", false)]
    [InlineData("deleted", true)]
    [InlineData("deleted", false)]
    public async Task AuthenticateAsync_StaleEmailCache_UsesCurrentAccountState(string change, bool useBasic)
    {
        var repository = (CachingUserRepository)GetService<IUserRepository>();
        var login = await SendRequestAsAsync<TokenResult>(r => r.Post().AppendPath("auth/login")
            .Content(new Login { Email = SampleDataService.TEST_USER_EMAIL, Password = SampleDataService.TEST_USER_PASSWORD })
            .StatusCodeShouldBeOk());
        Assert.NotNull(login);
        var stale = await repository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(stale);
        Assert.StartsWith("pbkdf2-sha256$", stale.Password);
        var current = change == "inactive" ? stale with { IsActive = false } : stale with { EmailAddress = "changed@example.test" };
        if (change == "password")
        {
            await SendRequestAsync(r => r.Post().AppendPath("auth/change-password").BearerToken(login.Token)
                .Content(new ChangePasswordModel { CurrentPassword = SampleDataService.TEST_USER_PASSWORD, Password = "ChangedPassword2$" })
                .StatusCodeShouldBeOk());
        }
        else if (change == "deleted")
        {
            await repository.RemoveAsync(stale, o => o.ImmediateConsistency());
        }
        else
        {
            await repository.SaveAsync(current, o => o.ImmediateConsistency());
        }

        // A delayed lookup can populate its old snapshot after write invalidation.
        await repository.CacheUserAsync(stale);
        var cached = await repository.GetByEmailAddressAsync(stale.EmailAddress);
        Assert.NotNull(cached);
        Assert.Equal(stale.Version, cached.Version);
        Assert.True(cached.IsActive);
        Assert.True(cached.IsCorrectPassword(SampleDataService.TEST_USER_PASSWORD));

        if (useBasic)
        {
            await SendRequestAsync(r => r.BasicAuthorization(stale.EmailAddress, SampleDataService.TEST_USER_PASSWORD)
                .AppendPath("users/me").StatusCodeShouldBeUnauthorized());
        }
        else
        {
            await SendRequestAsync(r => r.Post().AppendPath("auth/login")
                .Content(new Login { Email = stale.EmailAddress, Password = SampleDataService.TEST_USER_PASSWORD })
                .StatusCodeShouldBeUnauthorized());
        }

        if (change is "password" or "email")
        {
            await SendRequestAsync(r => r.BasicAuthorization(change == "email" ? current.EmailAddress : stale.EmailAddress,
                    change == "password" ? "ChangedPassword2$" : SampleDataService.TEST_USER_PASSWORD)
                .AppendPath("users/me").StatusCodeShouldBeOk());
        }
    }

    [Fact]
    public async Task AuthenticateAsync_StaleRoleCache_UsesCurrentPermissions()
    {
        var repository = (CachingUserRepository)GetService<IUserRepository>();
        await SendRequestAsync(r => r.AsGlobalAdminUser().AppendPath("users/me").StatusCodeShouldBeOk());
        var stale = await repository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(stale);
        Assert.Contains(AuthorizationRoles.GlobalAdmin, stale.Roles);
        var current = stale with { Roles = stale.Roles.Where(role => role != AuthorizationRoles.GlobalAdmin).ToHashSet() };
        await repository.SaveAsync(current, o => o.ImmediateConsistency());
        await repository.CacheUserAsync(stale);

        await SendRequestAsync(r => r.AsGlobalAdminUser().AppendPaths("admin", "assistant-settings").StatusCodeShouldBeForbidden());
        var login = await SendRequestAsAsync<TokenResult>(r => r.Post().AppendPath("auth/login")
            .Content(new Login { Email = stale.EmailAddress, Password = SampleDataService.TEST_USER_PASSWORD })
            .StatusCodeShouldBeOk());
        Assert.NotNull(login);
        await SendRequestAsync(r => r.BearerToken(login.Token).AppendPaths("admin", "assistant-settings").StatusCodeShouldBeForbidden());
    }

    private sealed class CachingUserRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options)
        : UserRepository(configuration, validator, options)
    {
        public Task CacheUserAsync(User user) => AddDocumentsToCacheAsync(user, new CommandOptions<User>().Cache(), false);
    }
}
