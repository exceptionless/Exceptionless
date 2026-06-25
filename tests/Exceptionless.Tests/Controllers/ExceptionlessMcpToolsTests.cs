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

    public ExceptionlessMcpToolsTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _eventRepository = GetService<IEventRepository>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _stackRepository = GetService<IStackRepository>();
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

        Assert.True(result.Ok);
        Assert.Null(result.Error);
        Assert.Contains(Items(result), p => p.Id == TestConstants.ProjectId);
    }

    [Fact]
    public async Task SearchStacksAsync_StacksScope_ReturnsProjectStacks()
    {
        var (stacks, _) = await CreateDataAsync(d => d.Event().TestProject().Message("MCP stack"));
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksRead);

        var result = await tools.SearchStacksAsync(TestConstants.ProjectId, limit: 50);

        Assert.True(result.Ok);
        Assert.Null(result.Error);
        Assert.Contains(Items(result), s => s.Id == stacks[0].Id);
    }

    [Fact]
    public async Task SearchEventsAsync_EventsScope_ReturnsProjectEvents()
    {
        const string referenceId = "mcp-search-events";
        var (_, events) = await CreateDataAsync(d => d.Event().TestProject().ReferenceId(referenceId).Message("MCP event search"));
        await RefreshDataAsync();
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.EventsRead);

        var result = await tools.SearchEventsAsync(TestConstants.ProjectId, filter: $"reference:{referenceId}", limit: 50);

        Assert.True(result.Ok);
        Assert.Null(result.Error);
        Assert.Contains(Items(result), e => e.Id == events[0].Id);
    }

    [Fact]
    public async Task GetEventAsync_EventsScope_ReturnsEvent()
    {
        var (_, events) = await CreateDataAsync(d => d.Event().TestProject().Message("MCP event"));
        await RefreshDataAsync();
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.EventsRead);

        var result = await tools.GetEventAsync(events[0].Id);

        Assert.True(result.Ok);
        Assert.Null(result.Error);
        Assert.Equal(events[0].Id, Data(result).Id);
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
        var item = Data(result);

        Assert.True(result.Ok);
        Assert.Null(result.Error);
        Assert.NotNull(item.Details);
        Assert.False(item.Details.IsTruncated);
        var error = Assert.IsType<SimpleError>(item.Details.Error);
        Assert.Equal("Boom", error.Message);
        Assert.Equal("at Test.Throw() in Test.cs:line 42", error.StackTrace);
        Assert.Equal("/broken", item.Details.Request?.Path);
        Assert.Equal("custom-value", item.Details.Data?["custom"]);
    }

    [Fact]
    public async Task SearchStacksAsync_MissingStacksScope_ReturnsError()
    {
        await CreateDataAsync(d => d.Event().TestProject().Message("MCP stack"));
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead);

        var result = await tools.SearchStacksAsync(TestConstants.ProjectId);

        Assert.False(result.Ok);
        Assert.Equal(McpErrorCodes.NotAccessible, result.Error?.Code);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task SearchStacksAsync_InvalidSort_ReturnsError()
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksRead);

        var result = await tools.SearchStacksAsync(TestConstants.ProjectId, sort: "-this_field_does_not_exist");

        Assert.False(result.Ok);
        Assert.Equal(McpErrorCodes.InvalidSort, result.Error?.Code);
        Assert.Contains("Unknown sort field", result.Error?.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task SearchStacksAsync_UnknownFilterField_ReturnsError()
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksRead);

        var result = await tools.SearchStacksAsync(TestConstants.ProjectId, filter: "nonexistentfield:foo");

        Assert.False(result.Ok);
        Assert.Equal(McpErrorCodes.UnknownFilterField, result.Error?.Code);
        Assert.Equal("Unknown filter field 'nonexistentfield'.", result.Error?.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task SearchStacksAsync_MalformedFilter_ReturnsSpecificError()
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksRead);

        var result = await tools.SearchStacksAsync(TestConstants.ProjectId, filter: "type:::((( garbage");

        Assert.False(result.Ok);
        Assert.Equal(McpErrorCodes.InvalidFilter, result.Error?.Code);
        Assert.StartsWith("Invalid filter:", result.Error?.Message);
        Assert.Null(result.Data);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task SearchStacksAsync_InvalidLimit_ReturnsError(int limit)
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksRead);

        var result = await tools.SearchStacksAsync(TestConstants.ProjectId, limit: limit);

        Assert.False(result.Ok);
        Assert.Equal(McpErrorCodes.InvalidLimit, result.Error?.Code);
        Assert.Equal("Limit must be between 1 and 100.", result.Error?.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task SearchStacksAsync_LimitAboveMaximum_IsCappedWithWarning()
    {
        await CreateDataAsync(d => d.Event().TestProject().Message("MCP stack"));
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksRead);

        var result = await tools.SearchStacksAsync(TestConstants.ProjectId, limit: 9999);

        Assert.True(result.Ok);
        Assert.Null(result.Error);
        Assert.Equal(100, result.Pagination?.Limit);
        Assert.Equal("Limit was capped at 100.", result.Warning);
    }

    [Fact]
    public async Task SearchEventsAsync_InvalidTimeRange_ReturnsError()
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.EventsRead);

        var result = await tools.SearchEventsAsync(TestConstants.ProjectId, last: "24h", startUtc: "2026-06-25T00:00:00Z");

        Assert.False(result.Ok);
        Assert.Equal(McpErrorCodes.InvalidTimeRange, result.Error?.Code);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task CountEventsAsync_LastAndInterval_ReturnsEventCounts()
    {
        const string referenceId = "mcp-count-events";
        await CreateDataAsync(d => d.Event().TestProject().ReferenceId(referenceId).Message("MCP count event"));
        await RefreshDataAsync();
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.EventsRead);

        var result = await tools.CountEventsAsync(TestConstants.ProjectId, filter: $"reference:{referenceId}", last: "24h", interval: "1h");
        var data = Data(result);

        Assert.True(result.Ok);
        Assert.Null(result.Error);
        Assert.True(data.Events >= 1);
        Assert.True(data.Occurrences >= 1);
        Assert.Equal("1h", data.Interval);
        Assert.NotEmpty(data.Trend);
    }

    [Fact]
    public async Task CountEventsAsync_InvalidInterval_ReturnsError()
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.EventsRead);

        var result = await tools.CountEventsAsync(TestConstants.ProjectId, interval: "garbage");

        Assert.False(result.Ok);
        Assert.Equal(McpErrorCodes.InvalidInterval, result.Error?.Code);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task ListProjectsAsync_MalformedFilter_ReturnsSpecificError()
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.ProjectsRead);

        var result = await tools.ListProjectsAsync(filter: "(((((");

        Assert.False(result.Ok);
        Assert.Equal(McpErrorCodes.InvalidFilter, result.Error?.Code);
        Assert.StartsWith("Invalid filter:", result.Error?.Message);
        Assert.Null(result.Data);
    }

    [Theory]
    [InlineData("bad-project-id")]
    [InlineData("")]
    public async Task GetProjectAsync_InvalidId_ReturnsInvalidId(string projectId)
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.ProjectsRead);

        var result = await tools.GetProjectAsync(projectId);

        Assert.False(result.Ok);
        Assert.Null(result.Data);
        Assert.Equal(McpErrorCodes.InvalidId, result.Error?.Code);
    }

    [Fact]
    public async Task SearchStacksAsync_InvalidProjectId_ReturnsInvalidId()
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksRead);

        var result = await tools.SearchStacksAsync("bad-project-id");

        Assert.False(result.Ok);
        Assert.Equal(McpErrorCodes.InvalidId, result.Error?.Code);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetEventAsync_InvalidEventId_ReturnsInvalidId()
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.EventsRead);

        var result = await tools.GetEventAsync("bad-event-id");

        Assert.False(result.Ok);
        Assert.Null(result.Data);
        Assert.Equal(McpErrorCodes.InvalidId, result.Error?.Code);
    }

    [Fact]
    public async Task GetEventAsync_DetailPayloadAboveMaximum_OmitsLargeDetails()
    {
        var (_, events) = await CreateDataAsync(d => d.Event().TestProject().Type(Event.KnownTypes.Error).Message("MCP large detail event"));
        var ev = events[0];
        ev.SetSimpleError(new SimpleError
        {
            Message = "Boom",
            Type = "System.InvalidOperationException",
            StackTrace = "at Test.Throw()"
        });
        ev.Data!["large"] = new string('x', 10_000);
        await _eventRepository.SaveAsync(ev, o => o.ImmediateConsistency());
        await RefreshDataAsync();
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.EventsRead);

        var result = await tools.GetEventAsync(ev.Id, maxDetailSize: 1024);
        var item = Data(result);

        Assert.True(result.Ok);
        Assert.NotNull(item.Details);
        Assert.True(item.Details.IsTruncated);
        Assert.Null(item.Details.Data);
        Assert.True(item.Details.Size > item.Details.MaxSize);
        Assert.NotNull(item.Details.TruncationMessage);
    }

    [Fact]
    public async Task GetEventAsync_InvalidMaxDetailSize_ReturnsError()
    {
        var (_, events) = await CreateDataAsync(d => d.Event().TestProject().Message("MCP detail size"));
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.EventsRead);

        var result = await tools.GetEventAsync(events[0].Id, maxDetailSize: 100);

        Assert.False(result.Ok);
        Assert.Equal(McpErrorCodes.InvalidDetailSize, result.Error?.Code);
    }

    [Fact]
    public async Task GetFilterFields_McpScope_ReturnsSupportedFields()
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead);

        var result = tools.GetFilterFields();
        var item = Data(result);

        Assert.True(result.Ok);
        Assert.Contains("name", item.Projects.FilterFields);
        Assert.Contains("status", item.Stacks.FilterFields);
        Assert.Contains("path", item.Events.FilterFields);
        Assert.Contains("data.", item.Stacks.DynamicFilterPrefixes);
    }

    [Fact]
    public async Task UpdateStackStatusAsync_StacksWriteScope_MarksFixedWithVersion()
    {
        var (stacks, _) = await CreateDataAsync(d => d.Event().TestProject().Message("MCP write fixed"));
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksWrite);

        var result = await tools.UpdateStackStatusAsync(stacks[0].Id, "fixed", "1.0.2");
        var data = Data(result);

        Assert.True(result.Ok);
        Assert.Null(result.Error);
        Assert.True(data.Changed);
        Assert.Equal("fixed", data.Stack.Status);
        Assert.Equal("1.0.2", data.Stack.FixedInVersion);
        Assert.NotNull(data.Stack.DateFixed);

        var stack = await _stackRepository.GetByIdAsync(stacks[0].Id, o => o.ImmediateConsistency());
        Assert.NotNull(stack);
        Assert.Equal(StackStatus.Fixed, stack.Status);
        Assert.Equal("1.0.2", stack.FixedInVersion);
        Assert.NotNull(stack.DateFixed);
    }

    [Fact]
    public async Task UpdateStackStatusAsync_MissingStacksWriteScope_ReturnsError()
    {
        var (stacks, _) = await CreateDataAsync(d => d.Event().TestProject().Message("MCP write missing scope"));
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksRead);

        var result = await tools.UpdateStackStatusAsync(stacks[0].Id, "fixed");

        Assert.False(result.Ok);
        Assert.Equal(McpErrorCodes.NotAccessible, result.Error?.Code);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task UpdateStackStatusAsync_InvalidStatus_ReturnsError()
    {
        var (stacks, _) = await CreateDataAsync(d => d.Event().TestProject().Message("MCP write invalid status"));
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksWrite);

        var result = await tools.UpdateStackStatusAsync(stacks[0].Id, "snoozed");

        Assert.False(result.Ok);
        Assert.Equal(McpErrorCodes.InvalidStatus, result.Error?.Code);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task SnoozeStackAsync_Duration_SnoozesStack()
    {
        try
        {
            TimeProvider.SetUtcNow(new DateTimeOffset(2026, 6, 25, 12, 0, 0, TimeSpan.Zero));
            var (stacks, _) = await CreateDataAsync(d => d.Event().TestProject().Message("MCP write snooze"));
            var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksWrite);

            var result = await tools.SnoozeStackAsync(stacks[0].Id, duration: "2h");
            var data = Data(result);

            Assert.True(result.Ok);
            Assert.Null(result.Error);
            Assert.True(data.Changed);
            Assert.Equal("snoozed", data.Stack.Status);
            Assert.Equal(new DateTime(2026, 6, 25, 14, 0, 0, DateTimeKind.Utc), data.Stack.SnoozeUntilUtc);

            var stack = await _stackRepository.GetByIdAsync(stacks[0].Id, o => o.ImmediateConsistency());
            Assert.NotNull(stack);
            Assert.Equal(StackStatus.Snoozed, stack.Status);
            Assert.Equal(new DateTime(2026, 6, 25, 14, 0, 0, DateTimeKind.Utc), stack.SnoozeUntilUtc);
        }
        finally
        {
            TimeProvider.Restore();
        }
    }

    [Fact]
    public async Task SnoozeStackAsync_TooShort_ReturnsError()
    {
        var (stacks, _) = await CreateDataAsync(d => d.Event().TestProject().Message("MCP write snooze short"));
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksWrite);

        var result = await tools.SnoozeStackAsync(stacks[0].Id, duration: "1m");

        Assert.False(result.Ok);
        Assert.Equal(McpErrorCodes.InvalidSnooze, result.Error?.Code);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task SetStackCriticalAsync_StacksWriteScope_TogglesCritical()
    {
        var (stacks, _) = await CreateDataAsync(d => d.Event().TestProject().Message("MCP write critical"));
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksWrite);

        var result = await tools.SetStackCriticalAsync(stacks[0].Id, critical: true);
        var data = Data(result);

        Assert.True(result.Ok);
        Assert.Null(result.Error);
        Assert.True(data.Changed);
        Assert.True(data.Stack.OccurrencesAreCritical);

        var stack = await _stackRepository.GetByIdAsync(stacks[0].Id, o => o.ImmediateConsistency());
        Assert.NotNull(stack);
        Assert.True(stack.OccurrencesAreCritical);
    }

    [Fact]
    public async Task GetStackEventsAsync_WithAfterCursor_ReturnsNextPage()
    {
        var (stacks, _) = await CreateDataAsync(d => d.Event().TestProject().Message("MCP cursor").Create(1));
        await RefreshDataAsync();
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.EventsRead);

        var firstPage = await tools.GetStackEventsAsync(stacks[0].Id, limit: 1);

        Assert.True(firstPage.Ok);
        Assert.Null(firstPage.Error);
        Assert.True(firstPage.Pagination?.HasMore);
        Assert.NotNull(firstPage.Pagination?.After);

        var secondPage = await tools.GetStackEventsAsync(stacks[0].Id, limit: 1, after: firstPage.Pagination.After);

        Assert.True(secondPage.Ok);
        Assert.Null(secondPage.Error);
        Assert.NotEmpty(Items(secondPage));
        Assert.DoesNotContain(Items(secondPage), e => e.Id == Items(firstPage).Single().Id);
    }

    [Theory]
    [InlineData(nameof(ExceptionlessMcpTools.ListProjectsAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.SearchStacksAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.GetStackEventsAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.SearchEventsAsync))]
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
        Assert.Contains("capped at 100", limitDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cursor", limitDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(nameof(ExceptionlessMcpTools.ListProjectsAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.SearchStacksAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.GetStackEventsAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.SearchEventsAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.CountEventsAsync))]
    public async Task FilteredTools_SchemaDescribesSupportedFilterFields(string methodName)
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.ProjectsRead, AuthorizationRoles.StacksRead, AuthorizationRoles.EventsRead);
        var method = typeof(ExceptionlessMcpTools).GetMethod(methodName) ?? throw new InvalidOperationException($"Could not find {methodName}.");
        var tool = McpServerTool.Create(method, tools, new McpServerToolCreateOptions());
        var properties = tool.ProtocolTool.InputSchema.GetProperty("properties");
        var filter = properties.GetProperty("filter");

        string? description = filter.GetProperty("description").GetString();
        Assert.Contains("Supported fields", description, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(nameof(ExceptionlessMcpTools.SearchStacksAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.GetStackEventsAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.SearchEventsAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.CountEventsAsync))]
    public async Task TimeScopedTools_SchemaIncludesTimeRangeParameters(string methodName)
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksRead, AuthorizationRoles.EventsRead);
        var method = typeof(ExceptionlessMcpTools).GetMethod(methodName) ?? throw new InvalidOperationException($"Could not find {methodName}.");
        var tool = McpServerTool.Create(method, tools, new McpServerToolCreateOptions());
        var properties = tool.ProtocolTool.InputSchema.GetProperty("properties");

        Assert.True(properties.TryGetProperty("last", out var last), "The last time range must be advertised in the MCP tool schema.");
        Assert.True(properties.TryGetProperty("startUtc", out var startUtc), "The startUtc time range must be advertised in the MCP tool schema.");
        Assert.True(properties.TryGetProperty("endUtc", out var endUtc), "The endUtc time range must be advertised in the MCP tool schema.");
        Assert.Contains("24h", last.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UTC", startUtc.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UTC", endUtc.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(nameof(ExceptionlessMcpTools.UpdateStackStatusAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.SnoozeStackAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.SetStackCriticalAsync))]
    public async Task StackWriteTools_SchemaAdvertisesWriteInputs(string methodName)
    {
        var tools = await CreateToolsAsync(AuthorizationRoles.McpRead, AuthorizationRoles.StacksWrite);
        var method = typeof(ExceptionlessMcpTools).GetMethod(methodName) ?? throw new InvalidOperationException($"Could not find {methodName}.");
        var tool = McpServerTool.Create(method, tools, new McpServerToolCreateOptions());
        var properties = tool.ProtocolTool.InputSchema.GetProperty("properties");

        Assert.True(properties.TryGetProperty("stackId", out _), "The stackId input must be advertised in the MCP tool schema.");
        switch (methodName)
        {
            case nameof(ExceptionlessMcpTools.UpdateStackStatusAsync):
                Assert.True(properties.TryGetProperty("status", out _), "The status input must be advertised in the MCP tool schema.");
                Assert.True(properties.TryGetProperty("fixedInVersion", out _), "The fixedInVersion input must be advertised in the MCP tool schema.");
                break;
            case nameof(ExceptionlessMcpTools.SnoozeStackAsync):
                Assert.True(properties.TryGetProperty("duration", out _), "The duration input must be advertised in the MCP tool schema.");
                Assert.True(properties.TryGetProperty("snoozeUntilUtc", out _), "The snoozeUntilUtc input must be advertised in the MCP tool schema.");
                break;
            case nameof(ExceptionlessMcpTools.SetStackCriticalAsync):
                Assert.True(properties.TryGetProperty("critical", out _), "The critical input must be advertised in the MCP tool schema.");
                break;
        }
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
            GetService<SemanticVersionParser>(),
            GetService<ITextSerializer>(),
            GetService<ILogger<ExceptionlessMcpTools>>(),
            TimeProvider));
    }

    private static IReadOnlyCollection<T> Items<T>(McpResponse<McpListData<T>> result)
    {
        Assert.NotNull(result.Data);
        return result.Data!.Items;
    }

    private static T Data<T>(McpResponse<T> result)
    {
        Assert.NotNull(result.Data);
        return result.Data!;
    }
}
