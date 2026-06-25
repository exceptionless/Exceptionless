using System.Security.Claims;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Mcp;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class ExceptionlessMcpToolsTests : IntegrationTestsBase
{
    private readonly IEventRepository _eventRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IStackRepository _stackRepository;
    private readonly IUserRepository _userRepository;

    public ExceptionlessMcpToolsTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _eventRepository = GetService<IEventRepository>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _stackRepository = GetService<IStackRepository>();
        _userRepository = GetService<IUserRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task ListProjectsAsync_ProjectsScope_ReturnsAccessibleProjects()
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.ProjectsRead);

        var result = await tools.ListProjectsAsync(limit: 50);

        Assert.Null(result.Error);
        Assert.Contains(result.Items, p => p.Id == TestConstants.ProjectId);
    }

    [Fact]
    public async Task SearchStacksAsync_StacksScope_ReturnsProjectStacks()
    {
        var (stacks, _) = await CreateDataAsync(d => d.Event().TestProject().Message("MCP stack"));
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksRead);

        var result = await tools.SearchStacksAsync(TestConstants.ProjectId, limit: 50);

        Assert.Null(result.Error);
        Assert.Contains(result.Items, s => s.Id == stacks[0].Id);
    }

    [Fact]
    public async Task GetEventAsync_EventsScope_ReturnsEvent()
    {
        var (_, events) = await CreateDataAsync(d => d.Event().TestProject().Message("MCP event"));
        await RefreshDataAsync();
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.EventsRead);

        var result = await tools.GetEventAsync(events[0].Id);

        Assert.True(result.Found);
        Assert.Null(result.Error);
        Assert.Equal(events[0].Id, result.Item?.Id);
    }

    [Fact]
    public async Task SearchStacksAsync_MissingStacksScope_ReturnsError()
    {
        await CreateDataAsync(d => d.Event().TestProject().Message("MCP stack"));
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead);

        var result = await tools.SearchStacksAsync(TestConstants.ProjectId);

        Assert.NotNull(result.Error);
        Assert.Empty(result.Items);
    }

    private Task<ExceptionlessMcpTools> CreateToolsAsync(params string[] scopes)
    {
        var user = new User
        {
            Id = TestConstants.UserId,
            FullName = "MCP Test User",
            EmailAddress = SampleDataService.TEST_USER_EMAIL
        };
        user.OrganizationIds.Add(TestConstants.OrganizationId);

        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var token = new Token
        {
            Id = "oauth-test-token",
            UserId = user.Id,
            Type = TokenType.Access,
            OAuthType = OAuthTokenType.Access,
            OAuthClientId = "test-client",
            OAuthResource = "http://localhost/mcp",
            Scopes = scopes.ToHashSet(StringComparer.Ordinal),
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow,
            CreatedBy = user.Id
        };

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(user.ToIdentity(token))
        };
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");

        return Task.FromResult(new ExceptionlessMcpTools(
            new HttpContextAccessor { HttpContext = context },
            _organizationRepository,
            _projectRepository,
            _stackRepository,
            _eventRepository));
    }
}