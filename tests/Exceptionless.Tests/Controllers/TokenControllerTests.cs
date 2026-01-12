using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using FluentRest;
using Foundatio.Repositories;
using Microsoft.AspNetCore.Mvc;
using Xunit;

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
    public async Task PreventAccessTokenForTokenActions()
    {
        var token = await SendRequestAsAsync<ViewToken>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("tokens")
            .Content(new NewToken
            {
                OrganizationId = SampleDataService.TEST_ORG_ID,
                ProjectId = SampleDataService.TEST_PROJECT_ID,
                Scopes = [AuthorizationRoles.Client, AuthorizationRoles.User]
            })
            .StatusCodeShouldBeCreated()
        );

        Assert.NotNull(token?.Id);
        Assert.Null(token.UserId);
        Assert.False(token.IsDisabled);
        Assert.Equal(2, token.Scopes.Count);

        await SendRequestAsync(r => r
            .Post()
            .BearerToken(token.Id)
            .AppendPath("tokens")
            .Content(new NewToken
            {
                OrganizationId = SampleDataService.TEST_ORG_ID,
                ProjectId = SampleDataService.TEST_PROJECT_ID,
                Scopes = [AuthorizationRoles.Client, AuthorizationRoles.User]
            })
            .StatusCodeShouldBeForbidden()
        );

        await SendRequestAsync(r => r
            .Patch()
            .BearerToken(token.Id)
            .AppendPath($"tokens/{token.Id}")
            .Content(new UpdateToken
            {
                IsDisabled = true,
                Notes = "Disabling until next release"
            })
            .StatusCodeShouldBeForbidden()
        );

        await SendRequestAsync(r => r
            .Delete()
            .BearerToken(token.Id)
            .AppendPath($"tokens/{token.Id}")
            .StatusCodeShouldBeForbidden()
        );

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
               Scopes = [AuthorizationRoles.Client, AuthorizationRoles.User]
           })
           .StatusCodeShouldBeCreated()
        );

        Assert.NotNull(token?.Id);
        Assert.Null(token.UserId);
        Assert.False(token.IsDisabled);
        Assert.Equal(2, token.Scopes.Count);

        var updateToken = new UpdateToken
        {
            IsDisabled = true,
            Notes = "Disabling until next release"
        };

        var updatedToken = await SendRequestAsAsync<ViewToken>(r => r
           .Patch()
           .AsTestOrganizationUser()
           .AppendPath($"tokens/{token.Id}")
           .Content(updateToken)
           .StatusCodeShouldBeOk()
        );

        Assert.NotNull(updatedToken);
        Assert.True(updatedToken.IsDisabled);
        Assert.Equal(updateToken.Notes, updatedToken.Notes);

        await SendRequestAsync(r => r
            .BearerToken(token.Id)
            .AppendPath("projects/config")
            .StatusCodeShouldBeUnauthorized()
        );

        var repository = GetService<ITokenRepository>();
        var actualToken = await repository.GetByIdAsync(token.Id);
        Assert.NotNull(actualToken);
        actualToken.IsDisabled = false;
        await repository.SaveAsync(actualToken, o => o.ImmediateConsistency());

        await SendRequestAsync(r => r
            .BearerToken(token.Id)
            .AppendPath("projects/config")
            .StatusCodeShouldBeOk()
        );
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
               Scopes = [AuthorizationRoles.Client]
           })
           .StatusCodeShouldBeCreated()
        );

        Assert.NotNull(token?.Id);
        Assert.Null(token.UserId);
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
           .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "suspend")
           .QueryString("code", SuspensionCode.Billing)
           .StatusCodeShouldBeOk()
        );

        var actualToken = await repository.GetByIdAsync(token.Id, o => o.Cache());
        Assert.NotNull(actualToken);
        Assert.True(actualToken.IsSuspended);

        await SendRequestAsync(r => r
           .Delete()
           .AsGlobalAdminUser()
           .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "suspend")
           .StatusCodeShouldBeOk()
        );

        actualToken = await repository.GetByIdAsync(token.Id, o => o.Cache());
        Assert.NotNull(actualToken);
        Assert.False(actualToken.IsSuspended);
    }

    [Fact]
    public async Task ShouldPreventAddingUserScopeToTokenWithoutElevatedRole()
    {
        var problemDetails = await SendRequestAsAsync<ValidationProblemDetails>(r => r
           .Post()
           .AsFreeOrganizationUser()
           .AppendPath("tokens")
           .Content(new NewToken
           {
               OrganizationId = SampleDataService.TEST_ORG_ID,
               ProjectId = SampleDataService.TEST_PROJECT_ID,
               Scopes = [AuthorizationRoles.Client, AuthorizationRoles.User, AuthorizationRoles.GlobalAdmin]
           })
           .StatusCodeShouldBeUnprocessableEntity()
        );

        Assert.NotNull(problemDetails);
        Assert.Single(problemDetails.Errors);
        Assert.Contains(problemDetails.Errors, error => String.Equals(error.Key, "scopes"));
    }
}
