using Exceptionless.Core.Authorization;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models.Admin;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class OAuthApplicationControllerTests : IntegrationTestsBase
{
    private readonly IOAuthApplicationRepository _repository;

    public OAuthApplicationControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _repository = GetService<IOAuthApplicationRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Fact]
    public async Task CreateAsync_AsGlobalAdmin_CreatesOAuthApplication()
    {
        var created = await CreateApplicationAsync(new NewOAuthApplication
        {
            ClientId = "chatgpt-dev",
            Name = "ChatGPT Dev",
            RedirectUris = ["https://chat.openai.com/aip/g-123/oauth/callback", "https://chatgpt.com/aip/g-123/oauth/callback"],
            Scopes = [AuthorizationRoles.McpRead, AuthorizationRoles.EventsRead.ToUpperInvariant(), AuthorizationRoles.EventsRead],
            Notes = " Dev OAuth app ",
            IsDisabled = false
        });

        Assert.NotNull(created);
        Assert.NotNull(created.Id);
        Assert.Equal("chatgpt-dev", created.ClientId);
        Assert.Equal("ChatGPT Dev", created.Name);
        Assert.Equal([OAuthGrantTypes.AuthorizationCode, OAuthGrantTypes.RefreshToken], created.GrantTypes);
        Assert.Equal(["mcp:read", "events:read"], created.Scopes);
        Assert.Equal("Dev OAuth app", created.Notes);
        Assert.False(created.IsDisabled);
        Assert.True(created.CreatedUtc > DateTime.MinValue);
        Assert.True(created.UpdatedUtc > DateTime.MinValue);

        var application = await _repository.GetByClientIdAsync("chatgpt-dev");
        Assert.NotNull(application);
        Assert.Equal(created.Id, application.Id);
    }

    [Fact]
    public async Task GetAllAsync_AsGlobalAdmin_ReturnsOAuthApplications()
    {
        var first = await CreateApplicationAsync(CreateModel("openai-dev", "OpenAI Dev"));
        var second = await CreateApplicationAsync(CreateModel("claude-dev", "Claude Dev"));
        Assert.NotNull(first);
        Assert.NotNull(second);

        var applications = await SendRequestAsAsync<IReadOnlyCollection<ViewOAuthApplication>>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("admin", "oauth-applications")
            .StatusCodeShouldBeOk());

        Assert.NotNull(applications);
        Assert.Contains(applications, a => a.Id == first.Id);
        Assert.Contains(applications, a => a.Id == second.Id);
    }

    [Fact]
    public async Task UpdateAsync_AsGlobalAdmin_UpdatesOAuthApplication()
    {
        var created = await CreateApplicationAsync(CreateModel("chatgpt-dev", "ChatGPT Dev"));
        Assert.NotNull(created);

        var updated = await SendRequestAsAsync<ViewOAuthApplication>(r => r
            .Put()
            .AsGlobalAdminUser()
            .AppendPaths("admin", "oauth-applications", created.Id)
            .Content(new UpdateOAuthApplication
            {
                ClientId = "chatgpt-production",
                Name = "ChatGPT Production",
                GrantTypes = [OAuthGrantTypes.AuthorizationCode],
                RedirectUris = ["https://chat.openai.com/aip/g-production/oauth/callback"],
                Scopes = [AuthorizationRoles.McpRead, AuthorizationRoles.ProjectsRead],
                Notes = "Production client",
                IsDisabled = true
            })
            .StatusCodeShouldBeOk());

        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("chatgpt-production", updated.ClientId);
        Assert.Equal("ChatGPT Production", updated.Name);
        Assert.Equal([OAuthGrantTypes.AuthorizationCode], updated.GrantTypes);
        Assert.Equal(["mcp:read", "projects:read"], updated.Scopes);
        Assert.True(updated.IsDisabled);

        var application = await _repository.GetByIdAsync(created.Id);
        Assert.NotNull(application);
        Assert.Equal("chatgpt-production", application.ClientId);
        Assert.True(application.IsDisabled);
    }

    [Fact]
    public async Task DeleteAsync_AsGlobalAdmin_RemovesOAuthApplication()
    {
        var created = await CreateApplicationAsync(CreateModel("chatgpt-dev", "ChatGPT Dev"));
        Assert.NotNull(created);

        await SendRequestAsync(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPaths("admin", "oauth-applications", created.Id)
            .StatusCodeShouldBeNoContent());

        var application = await _repository.GetByIdAsync(created.Id);
        Assert.Null(application);
    }

    [Fact]
    public async Task CreateAsync_DuplicateClientId_ReturnsUnprocessableEntity()
    {
        var created = await CreateApplicationAsync(CreateModel("chatgpt-dev", "ChatGPT Dev"));
        Assert.NotNull(created);

        var problem = await SendRequestAsAsync<ValidationProblemDetails>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("admin", "oauth-applications")
            .Content(CreateModel("chatgpt-dev", "Duplicate"))
            .StatusCodeShouldBeUnprocessableEntity());

        Assert.NotNull(problem);
        Assert.Contains("client_id", problem.Errors.Keys);
    }

    [Fact]
    public async Task CreateAsync_InsecureRedirectUri_ReturnsUnprocessableEntity()
    {
        var problem = await SendRequestAsAsync<ValidationProblemDetails>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("admin", "oauth-applications")
            .Content(new NewOAuthApplication
            {
                ClientId = "bad-client",
                Name = "Bad Client",
                RedirectUris = ["http://attacker.example/callback"],
                Scopes = [AuthorizationRoles.McpRead],
                Notes = null,
                IsDisabled = false
            })
            .StatusCodeShouldBeUnprocessableEntity());

        Assert.NotNull(problem);
        Assert.Contains("redirect_uris", problem.Errors.Keys);
    }

    [Fact]
    public async Task CreateAsync_DeviceOnlyClientWithoutRedirectUri_CreatesOAuthApplication()
    {
        var created = await CreateApplicationAsync(new NewOAuthApplication
        {
            ClientId = "device-client",
            Name = "Device Client",
            GrantTypes = [OAuthGrantTypes.DeviceCode, OAuthGrantTypes.RefreshToken],
            RedirectUris = [],
            Scopes = [AuthorizationRoles.McpRead, AuthorizationRoles.OfflineAccess],
            Notes = null,
            IsDisabled = false
        });

        Assert.NotNull(created);
        Assert.Empty(created.RedirectUris);
        Assert.Equal([OAuthGrantTypes.DeviceCode, OAuthGrantTypes.RefreshToken], created.GrantTypes);
    }

    [Fact]
    public async Task CreateAsync_AuthorizationCodeClientWithoutRedirectUri_ReturnsUnprocessableEntity()
    {
        var problem = await SendRequestAsAsync<ValidationProblemDetails>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("admin", "oauth-applications")
            .Content(new NewOAuthApplication
            {
                ClientId = "bad-redirect-client",
                Name = "Bad Redirect Client",
                GrantTypes = [OAuthGrantTypes.AuthorizationCode],
                RedirectUris = [],
                Scopes = [AuthorizationRoles.McpRead],
                Notes = null,
                IsDisabled = false
            })
            .StatusCodeShouldBeUnprocessableEntity());

        Assert.NotNull(problem);
        Assert.Contains("redirect_uris", problem.Errors.Keys);
    }

    [Fact]
    public Task GetAllAsync_AsOrganizationUser_ReturnsForbidden()
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("admin", "oauth-applications")
            .StatusCodeShouldBeForbidden());
    }

    private Task<ViewOAuthApplication?> CreateApplicationAsync(NewOAuthApplication model)
    {
        return SendRequestAsAsync<ViewOAuthApplication>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("admin", "oauth-applications")
            .Content(model)
            .StatusCodeShouldBeCreated());
    }

    private static NewOAuthApplication CreateModel(string clientId, string name)
    {
        return new NewOAuthApplication
        {
            ClientId = clientId,
            Name = name,
            GrantTypes = [OAuthGrantTypes.AuthorizationCode, OAuthGrantTypes.RefreshToken],
            RedirectUris = ["https://chat.openai.com/aip/g-test/oauth/callback"],
            Scopes = [AuthorizationRoles.McpRead, AuthorizationRoles.ProjectsRead],
            Notes = null,
            IsDisabled = false
        };
    }
}
