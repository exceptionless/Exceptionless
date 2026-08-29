using Exceptionless.Core.Authorization;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Models.Admin;
using Foundatio.Repositories;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class OAuthApplicationEndpointTests : IntegrationTestsBase
{
    private readonly IOAuthApplicationRepository _repository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly BillingPlans _plans;

    public OAuthApplicationEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _repository = GetService<IOAuthApplicationRepository>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _plans = GetService<BillingPlans>();
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
        Assert.Equal(["mcp:read", "events:read"], created.Scopes);
        Assert.Equal("Dev OAuth app", created.Notes);
        Assert.False(created.IsDisabled);
        Assert.Empty(created.Organizations);
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
    public async Task GetAllAsync_WithoutLimit_PreservesLegacyPageSize()
    {
        for (int i = 0; i < 21; i++)
            Assert.NotNull(await CreateApplicationAsync(CreateModel($"legacy-default-{i:D2}", $"Legacy Default {i:D2}")));

        var applications = await SendRequestAsAsync<IReadOnlyCollection<ViewOAuthApplication>>(request => request
            .AsGlobalAdminUser()
            .AppendPaths("admin", "oauth-applications")
            .QueryString("criteria", "Legacy Default")
            .StatusCodeShouldBeOk());

        Assert.NotNull(applications);
        Assert.Equal(21, applications.Count);
        Assert.Equal(
            Enumerable.Range(0, 21).Select(index => $"Legacy Default {index:D2}"),
            applications.Select(application => application.Name));
    }

    [Fact]
    public async Task GetAllAsync_WithPagingAndFilters_ReturnsMatchingOrganizationDetails()
    {
        var first = await CreateApplicationAsync(CreateModel("paged-oauth-alpha", "Paged OAuth Alpha"));
        var second = await CreateApplicationAsync(CreateModel("paged-oauth-beta", "Paged OAuth Beta"));
        Assert.NotNull(first);
        Assert.NotNull(second);

        var firstApplication = await _repository.GetByIdAsync(first.Id);
        Assert.NotNull(firstApplication);
        firstApplication.OrganizationIds.Add(SampleDataService.TEST_ORG_ID);
        await _repository.SaveAsync(firstApplication, options => options.ImmediateConsistency());

        var firstPage = await SendRequestAsAsync<IReadOnlyCollection<ViewOAuthApplication>>(request => request
            .AsGlobalAdminUser()
            .AppendPaths("admin", "oauth-applications")
            .QueryString("criteria", "Paged OAuth")
            .QueryString("page", 1)
            .QueryString("limit", 1)
            .StatusCodeShouldBeOk());
        var secondPage = await SendRequestAsAsync<IReadOnlyCollection<ViewOAuthApplication>>(request => request
            .AsGlobalAdminUser()
            .AppendPaths("admin", "oauth-applications")
            .QueryString("criteria", "Paged OAuth")
            .QueryString("page", 2)
            .QueryString("limit", 1)
            .StatusCodeShouldBeOk());

        Assert.NotNull(firstPage);
        Assert.NotNull(secondPage);
        Assert.Single(firstPage);
        Assert.Single(secondPage);
        Assert.NotEqual(firstPage.Single().Id, secondPage.Single().Id);

        var organizationMatches = await SendRequestAsAsync<IReadOnlyCollection<ViewOAuthApplication>>(request => request
            .AsGlobalAdminUser()
            .AppendPaths("admin", "oauth-applications")
            .QueryString("organization", "Acme")
            .QueryString("criteria", "paged-oauth")
            .StatusCodeShouldBeOk());

        Assert.NotNull(organizationMatches);
        var match = Assert.Single(organizationMatches);
        Assert.Equal(first.Id, match.Id);
        var organization = Assert.Single(match.Organizations);
        Assert.Equal(SampleDataService.TEST_ORG_ID, organization.Id);
        Assert.Equal("Acme", organization.Name);
    }

    [Fact]
    public async Task GetAllAsync_WithOrganizationFilter_ResolvesMatchesBeyondFirstPage()
    {
        var organizations = Enumerable.Range(0, Pagination.MaximumLimit * 11)
            .Select(index => new Organization
            {
                Name = $"OAuth Filter Organization {index:D3}",
                PlanId = _plans.FreePlan.Id
            })
            .ToArray();
        await _organizationRepository.AddAsync(organizations, options => options.ImmediateConsistency());

        var created = await CreateApplicationAsync(CreateModel("deep-organization-filter", "Deep Organization Filter"));
        Assert.NotNull(created);
        var application = await _repository.GetByIdAsync(created.Id);
        Assert.NotNull(application);
        application.OrganizationIds.Add(organizations[^1].Id);
        await _repository.SaveAsync(application, options => options.ImmediateConsistency());

        var applications = await SendRequestAsAsync<IReadOnlyCollection<ViewOAuthApplication>>(request => request
            .AsGlobalAdminUser()
            .AppendPaths("admin", "oauth-applications")
            .QueryString("organization", "OAuth Filter Organization")
            .QueryString("criteria", "deep-organization-filter")
            .StatusCodeShouldBeOk());

        Assert.NotNull(applications);
        var match = Assert.Single(applications);
        Assert.Equal(created.Id, match.Id);
        Assert.Contains(match.Organizations, organization => organization.Id == organizations[^1].Id);
    }

    [Fact]
    public async Task GetByIdAsync_AsGlobalAdmin_ReturnsOAuthApplication()
    {
        var created = await CreateApplicationAsync(CreateModel("oauth-by-id", "OAuth By Id"));
        Assert.NotNull(created);

        var application = await SendRequestAsAsync<ViewOAuthApplication>(request => request
            .AsGlobalAdminUser()
            .AppendPaths("admin", "oauth-applications", created.Id)
            .StatusCodeShouldBeOk());

        Assert.NotNull(application);
        Assert.Equal(created.Id, application.Id);
        Assert.Equal(created.ClientId, application.ClientId);
    }

    [Fact]
    public async Task UpdateAsync_AsGlobalAdmin_UpdatesOAuthApplication()
    {
        var created = await CreateApplicationAsync(CreateModel("chatgpt-dev", "ChatGPT Dev"));
        Assert.NotNull(created);
        var persistedApplication = await _repository.GetByIdAsync(created.Id);
        Assert.NotNull(persistedApplication);
        persistedApplication.OrganizationIds.Add(SampleDataService.TEST_ORG_ID);
        await _repository.SaveAsync(persistedApplication, options => options.ImmediateConsistency());

        var updated = await SendRequestAsAsync<ViewOAuthApplication>(r => r
            .Put()
            .AsGlobalAdminUser()
            .AppendPaths("admin", "oauth-applications", created.Id)
            .Content(new UpdateOAuthApplication
            {
                ClientId = "chatgpt-production",
                Name = "ChatGPT Production",
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
        Assert.Equal(["mcp:read", "projects:read"], updated.Scopes);
        Assert.True(updated.IsDisabled);
        var organization = Assert.Single(updated.Organizations);
        Assert.Equal(SampleDataService.TEST_ORG_ID, organization.Id);
        Assert.Equal("Acme", organization.Name);

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
            RedirectUris = ["https://chat.openai.com/aip/g-test/oauth/callback"],
            Scopes = [AuthorizationRoles.McpRead, AuthorizationRoles.ProjectsRead],
            Notes = null,
            IsDisabled = false
        };
    }
}
