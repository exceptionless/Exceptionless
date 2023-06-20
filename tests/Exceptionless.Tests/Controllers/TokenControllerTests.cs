using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using FluentRest;
using Foundatio.Repositories;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Controllers;

public sealed class TokenControllerTests : IntegrationTestsBase
{
    public TokenControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Fact]
    public async Task CanDisableApiKey()
    {
        var token = await SendRequestAsAsync<ViewToken>(r => r
           .Post()
           .AsGlobalAdminUser()
           .AppendPath("tokens")
           .Content(new NewToken
           {
               OrganizationId = SampleDataService.TEST_ORG_ID,
               ProjectId = SampleDataService.TEST_PROJECT_ID,
               Scopes = new HashSet<string> { AuthorizationRoles.Client, AuthorizationRoles.User }
           })
           .StatusCodeShouldBeCreated()
        );

        Assert.NotNull(token.Id);
        Assert.False(token.IsDisabled);
        Assert.Equal(2, token.Scopes.Count);

        var updateToken = new UpdateToken
        {
            IsDisabled = true,
            Notes = "Disabling until next release"
        };

        var updatedToken = await SendRequestAsAsync<ViewToken>(r => r
           .Patch()
           .BearerToken(token.Id)
           .AppendPath($"tokens/{token.Id}")
           .Content(updateToken)
           .StatusCodeShouldBeOk()
        );

        Assert.True(updatedToken.IsDisabled);
        Assert.Equal(updateToken.Notes, updatedToken.Notes);

        await SendRequestAsync(r => r
           .BearerToken(token.Id)
           .AppendPath($"tokens/{token.Id}")
           .StatusCodeShouldBeUnauthorized()
        );

        var repository = GetService<ITokenRepository>();
        var actualToken = await repository.GetByIdAsync(token.Id);
        Assert.NotNull(actualToken);
        actualToken.IsDisabled = false;
        await repository.SaveAsync(actualToken);

        token = await SendRequestAsAsync<ViewToken>(r => r
           .BearerToken(token.Id)
           .AppendPath($"tokens/{token.Id}")
           .StatusCodeShouldBeOk()
        );

        Assert.False(token.IsDisabled);
    }

    [Fact]
    public async Task SuspendingOrganizationWillDisableApiKey()
    {
        var token = await SendRequestAsAsync<ViewToken>(r => r
           .Post()
           .AsGlobalAdminUser()
           .AppendPath("tokens")
           .Content(new NewToken
           {
               OrganizationId = SampleDataService.TEST_ORG_ID,
               ProjectId = SampleDataService.TEST_PROJECT_ID,
               Scopes = new HashSet<string> { AuthorizationRoles.Client }
           })
           .StatusCodeShouldBeCreated()
        );

        Assert.NotNull(token.Id);
        Assert.False(token.IsDisabled);
        Assert.Single(token.Scopes);

        var repository = GetService<ITokenRepository>();
        var tokenRecord = await repository.GetByIdAsync(token.Id, o => o.Cache());

        Assert.NotNull(tokenRecord.Id);
        Assert.False(tokenRecord.IsDisabled);
        Assert.False(tokenRecord.IsSuspended);
        Assert.Single(tokenRecord.Scopes);

        await SendRequestAsync(r => r
           .Post()
           .AsGlobalAdminUser()
           .AppendPath($"organizations", SampleDataService.TEST_ORG_ID, "suspend")
           .QueryString("code", SuspensionCode.Billing)
           .StatusCodeShouldBeOk()
        );

        var actualToken = await repository.GetByIdAsync(token.Id, o => o.Cache());
        Assert.NotNull(actualToken);
        Assert.True(actualToken.IsSuspended);

        await SendRequestAsync(r => r
           .Delete()
           .AsGlobalAdminUser()
           .AppendPath($"organizations", SampleDataService.TEST_ORG_ID, "suspend")
           .StatusCodeShouldBeOk()
        );

        actualToken = await repository.GetByIdAsync(token.Id, o => o.Cache());
        Assert.NotNull(actualToken);
        Assert.False(actualToken.IsSuspended);
    }
}
