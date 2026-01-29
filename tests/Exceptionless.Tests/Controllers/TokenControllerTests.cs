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
    private readonly ITokenRepository _tokenRepository;

    public TokenControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _tokenRepository = GetService<ITokenRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Fact]
    public async Task PostAsync_NewToken_MapsAllPropertiesToToken()
    {
        // Arrange
        var newToken = new NewToken
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = SampleDataService.TEST_PROJECT_ID,
            Scopes = [AuthorizationRoles.Client],
            Notes = "Mapped test token"
        };

        // Act
        var viewToken = await SendRequestAsAsync<ViewToken>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("tokens")
            .Content(newToken)
            .StatusCodeShouldBeCreated()
        );

        // Assert
        Assert.NotNull(viewToken);
        Assert.NotNull(viewToken.Id);
        Assert.Equal(SampleDataService.TEST_ORG_ID, viewToken.OrganizationId);
        Assert.Equal(SampleDataService.TEST_PROJECT_ID, viewToken.ProjectId);
        Assert.Contains(AuthorizationRoles.Client, viewToken.Scopes);
        Assert.Equal("Mapped test token", viewToken.Notes);
        Assert.True(viewToken.CreatedUtc > DateTime.MinValue);

        // Verify persisted
        var token = await _tokenRepository.GetByIdAsync(viewToken.Id);
        Assert.NotNull(token);
        Assert.Equal("Mapped test token", token.Notes);
    }

    [Fact]
    public async Task GetAsync_ExistingToken_MapsToViewToken()
    {
        // Arrange
        var newToken = new NewToken
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = SampleDataService.TEST_PROJECT_ID,
            Scopes = [AuthorizationRoles.Client],
            Notes = "Get test token"
        };

        var createdToken = await SendRequestAsAsync<ViewToken>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("tokens")
            .Content(newToken)
            .StatusCodeShouldBeCreated()
        );

        Assert.NotNull(createdToken);

        // Act
        var viewToken = await SendRequestAsAsync<ViewToken>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("tokens", createdToken.Id)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(viewToken);
        Assert.Equal(createdToken.Id, viewToken.Id);
        Assert.Equal(createdToken.OrganizationId, viewToken.OrganizationId);
        Assert.Equal(createdToken.ProjectId, viewToken.ProjectId);
        Assert.Equal(createdToken.Notes, viewToken.Notes);
    }

    [Fact]
    public async Task PostAsync_NewTokenWithExpiry_MapsExpiresUtc()
    {
        // Arrange
        var expiryDate = DateTime.UtcNow.AddDays(30);
        var newToken = new NewToken
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = SampleDataService.TEST_PROJECT_ID,
            Scopes = [AuthorizationRoles.Client],
            ExpiresUtc = expiryDate
        };

        // Act
        var viewToken = await SendRequestAsAsync<ViewToken>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("tokens")
            .Content(newToken)
            .StatusCodeShouldBeCreated()
        );

        // Assert
        Assert.NotNull(viewToken);
        Assert.NotNull(viewToken.ExpiresUtc);
        Assert.True(viewToken.ExpiresUtc.Value > DateTime.UtcNow);
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

    [Fact]
    public async Task PostByOrganizationAsync_WithUnauthorizedOrganizationId_ReturnsBadRequest()
    {
        // Arrange
        var newToken = new NewToken
        {
            OrganizationId = SampleDataService.FREE_ORG_ID,
            Scopes = [AuthorizationRoles.Client]
        };

        // Act
        var response = await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "tokens")
            .Content(newToken)
            .StatusCodeShouldBeBadRequest()
        );

        // Assert
        Assert.NotNull(response);
    }

    [Fact]
    public async Task PostAsync_WithProjectFromUnauthorizedOrganization_ReturnsValidationError()
    {
        // Arrange
        var newToken = new NewToken
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = SampleDataService.FREE_PROJECT_ID,
            Scopes = [AuthorizationRoles.Client]
        };

        // Act
        var problemDetails = await SendRequestAsAsync<ValidationProblemDetails>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("tokens")
            .Content(newToken)
            .StatusCodeShouldBeUnprocessableEntity()
        );

        // Assert
        Assert.NotNull(problemDetails);
        Assert.Contains(problemDetails.Errors, error => error.Key.Equals("project_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PostAsync_WithMismatchedOrgAndProjectId_UsesProjectOrganization()
    {
        // Arrange - Global admin creates token with wrong org but valid project
        // The system should overwrite OrganizationId to match the project's actual org
        var newToken = new NewToken
        {
            OrganizationId = SampleDataService.FREE_ORG_ID,
            ProjectId = SampleDataService.TEST_PROJECT_ID,
            Scopes = [AuthorizationRoles.Client]
        };

        // Act
        var viewToken = await SendRequestAsAsync<ViewToken>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("tokens")
            .Content(newToken)
            .StatusCodeShouldBeCreated()
        );

        // Assert - OrganizationId should be overwritten to match the project's org
        Assert.NotNull(viewToken);
        Assert.Equal(SampleDataService.TEST_ORG_ID, viewToken.OrganizationId);
        Assert.Equal(SampleDataService.TEST_PROJECT_ID, viewToken.ProjectId);
    }

    [Fact]
    public async Task PostAsync_WithInvalidOrganizationIdFormat_ReturnsValidationError()
    {
        // Arrange
        var newToken = new NewToken
        {
            OrganizationId = "invalid-org-id",
            Scopes = [AuthorizationRoles.Client]
        };

        // Act
        var problemDetails = await SendRequestAsAsync<ValidationProblemDetails>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("tokens")
            .Content(newToken)
            .StatusCodeShouldBeUnprocessableEntity()
        );

        // Assert
        Assert.NotNull(problemDetails);
        Assert.Contains(problemDetails.Errors, error => error.Key.Equals("organization_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PostAsync_WithInvalidProjectIdFormat_ReturnsValidationError()
    {
        // Arrange
        var newToken = new NewToken
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = "invalid-project-id",
            Scopes = [AuthorizationRoles.Client]
        };

        // Act
        var problemDetails = await SendRequestAsAsync<ValidationProblemDetails>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("tokens")
            .Content(newToken)
            .StatusCodeShouldBeUnprocessableEntity()
        );

        // Assert
        Assert.NotNull(problemDetails);
        Assert.Contains(problemDetails.Errors, error => error.Key.Equals("project_id", StringComparison.OrdinalIgnoreCase));
    }
}
