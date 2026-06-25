using System.Security.Claims;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Mcp;
using Foundatio.Repositories;
using Foundatio.Repositories.Extensions;
using Foundatio.Serializer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
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
    public async Task GetEventAsync_EventsScope_ReturnsEventDetails()
    {
        var (_, events) = await CreateDataAsync(d => d.Event().TestProject().Type(Event.KnownTypes.Error).Message("MCP detail event"));
        var ev = events[0];
        ev.SetSimpleError(new SimpleError
        {
            Message = "Boom",
            Type = "System.InvalidOperationException",
            StackTrace = "at Test.Throw() in Test.cs:line 42"
        });
        ev.Data![Event.KnownDataKeys.RequestInfo] = new RequestInfo
        {
            HttpMethod = "GET",
            Path = "/broken"
        };
        ev.Data!["custom"] = "custom-value";
        await _eventRepository.SaveAsync(ev, o => o.ImmediateConsistency());
        await RefreshDataAsync();
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.EventsRead);

        var result = await tools.GetEventAsync(ev.Id);

        Assert.True(result.Found);
        Assert.Null(result.Error);
        Assert.NotNull(result.Item?.Details);
        var error = Assert.IsType<SimpleError>(result.Item.Details.Error);
        Assert.Equal("Boom", error.Message);
        Assert.Equal("at Test.Throw() in Test.cs:line 42", error.StackTrace);
        Assert.Equal("/broken", result.Item.Details.Request?.Path);
        Assert.Equal("custom-value", result.Item.Details.Data?["custom"]);
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

    [Fact]
    public async Task SearchStacksAsync_InvalidSort_ReturnsError()
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksRead);

        var result = await tools.SearchStacksAsync(TestConstants.ProjectId, sort: "-this_field_does_not_exist");

        Assert.Contains("Unknown sort field", result.Error);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task SearchStacksAsync_UnknownFilterField_ReturnsError()
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksRead);

        var result = await tools.SearchStacksAsync(TestConstants.ProjectId, filter: "nonexistentfield:foo");

        Assert.Equal("Unknown filter field 'nonexistentfield'.", result.Error);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task SearchStacksAsync_MalformedFilter_ReturnsSpecificError()
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksRead);

        var result = await tools.SearchStacksAsync(TestConstants.ProjectId, filter: "type:::((( garbage");

        Assert.StartsWith("Invalid filter:", result.Error);
        Assert.Empty(result.Items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task SearchStacksAsync_InvalidLimit_ReturnsError(int limit)
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksRead);

        var result = await tools.SearchStacksAsync(TestConstants.ProjectId, limit: limit);

        Assert.Equal("Limit must be between 1 and 50.", result.Error);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task SearchStacksAsync_LimitAboveMaximum_IsCappedWithWarning()
    {
        await CreateDataAsync(d => d.Event().TestProject().Message("MCP stack"));
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksRead);

        var result = await tools.SearchStacksAsync(TestConstants.ProjectId, limit: 9999);

        Assert.Null(result.Error);
        Assert.Equal(50, result.Limit);
        Assert.Equal("Limit was capped at 50.", result.Warning);
    }

    [Fact]
    public async Task GetStackEventsAsync_WithAfterCursor_ReturnsNextPage()
    {
        var (stacks, _) = await CreateDataAsync(d => d.Event().TestProject().Message("MCP cursor").Create(1));
        await RefreshDataAsync();
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.EventsRead);

        var firstPage = await tools.GetStackEventsAsync(stacks[0].Id, limit: 1);

        Assert.Null(firstPage.Error);
        Assert.True(firstPage.HasMore);
        Assert.NotNull(firstPage.After);

        var secondPage = await tools.GetStackEventsAsync(stacks[0].Id, limit: 1, after: firstPage.After);

        Assert.Null(secondPage.Error);
        Assert.NotEmpty(secondPage.Items);
        Assert.DoesNotContain(secondPage.Items, e => e.Id == firstPage.Items.Single().Id);
    }

    [Theory]
    [InlineData(nameof(ExceptionlessMcpTools.ListProjectsAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.SearchStacksAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.GetStackEventsAsync))]
    public async Task PagedTools_SchemaIncludesCursorParameters(string methodName)
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.ProjectsRead, AuthorizationRoles.StacksRead, AuthorizationRoles.EventsRead);
        var method = typeof(ExceptionlessMcpTools).GetMethod(methodName) ?? throw new InvalidOperationException($"Could not find {methodName}.");
        var tool = McpServerTool.Create(method, tools, new McpServerToolCreateOptions());
        var properties = tool.ProtocolTool.InputSchema.GetProperty("properties");

        Assert.True(properties.TryGetProperty("after", out var after), "The after cursor must be advertised in the MCP tool schema.");
        Assert.True(properties.TryGetProperty("before", out var before), "The before cursor must be advertised in the MCP tool schema.");
        Assert.Contains("cursor", after.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cursor", before.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);

        var limit = properties.GetProperty("limit");
        string? limitDescription = limit.GetProperty("description").GetString();
        Assert.Contains("capped at 50", limitDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cursor", limitDescription, StringComparison.OrdinalIgnoreCase);
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
            _eventRepository,
            GetService<StackQueryValidator>(),
            GetService<PersistentEventQueryValidator>(),
            GetService<ITextSerializer>(),
            GetService<ILogger<ExceptionlessMcpTools>>()));
    }
}
