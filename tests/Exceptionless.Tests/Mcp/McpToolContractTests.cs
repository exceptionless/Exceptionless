using System.Text.Json;
using Exceptionless.Web.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using Xunit;

namespace Exceptionless.Tests.Mcp;

public sealed class McpToolContractTests
{
    [Fact]
    public async Task ValidateProjectScopeAsync_DirectResourceWithoutProjectContext_Succeeds()
    {
        var service = new McpContextService(null!, null!, null!, null);

        var error = await service.ValidateProjectScopeAsync("organization-id", "project-id", requestedProjectId: null);

        Assert.Null(error);
    }

    [Fact]
    public void ContextErrors_IncludeMachineReadableRecoveryGuidance()
    {
        var required = McpErrors.ContextRequired("Select a project.", "project", [], []);
        var mismatch = McpErrors.ContextMismatch("Project mismatch.", "active-organization", "requested-organization", "active-project", "requested-project");

        Assert.Contains("projectId", Assert.IsType<string>(required.Details?["recovery"]), StringComparison.Ordinal);
        Assert.Contains("omit optional projectId", Assert.IsType<string>(mismatch.Details?["recovery"]), StringComparison.Ordinal);
        Assert.Equal("active-project", mismatch.Details?["activeProjectId"]);
        Assert.Equal("requested-project", mismatch.Details?["requestedProjectId"]);
    }

    [Theory]
    [InlineData(nameof(ExceptionlessMcpTools.GetEventAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.GetStackAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.GetStackEventsAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.UpdateStackStatusAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.SnoozeStackAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.SetStackCriticalAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.AddStackReferenceLinkAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.RemoveStackReferenceLinkAsync))]
    public void DirectResourceTools_AdvertiseProjectAsAnOptionalConsistencyCheck(string methodName)
    {
        var protocolTool = CreateProtocolTool(methodName);
        var projectId = protocolTool.InputSchema.GetProperty("properties").GetProperty("projectId");

        Assert.Contains("not required", projectId.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("projectId", RequiredProperties(protocolTool.InputSchema));
    }

    [Theory]
    [InlineData(nameof(ExceptionlessMcpTools.UpdateStackStatusAsync), true, true)]
    [InlineData(nameof(ExceptionlessMcpTools.SnoozeStackAsync), true, false)]
    [InlineData(nameof(ExceptionlessMcpTools.SetStackCriticalAsync), true, true)]
    [InlineData(nameof(ExceptionlessMcpTools.AddStackReferenceLinkAsync), false, true)]
    [InlineData(nameof(ExceptionlessMcpTools.RemoveStackReferenceLinkAsync), true, true)]
    public void StackWriteTools_AdvertiseSideEffectHints(string methodName, bool destructive, bool idempotent)
    {
        var annotations = Assert.IsType<ModelContextProtocol.Protocol.ToolAnnotations>(CreateProtocolTool(methodName).Annotations);

        Assert.False(annotations.ReadOnlyHint);
        Assert.Equal(destructive, annotations.DestructiveHint);
        Assert.Equal(idempotent, annotations.IdempotentHint);
        Assert.False(annotations.OpenWorldHint);
        Assert.False(String.IsNullOrWhiteSpace(annotations.Title));
    }

    [Fact]
    public void SearchStacks_SchemaRoutesKnownIdsToDirectLookup()
    {
        var protocolTool = CreateProtocolTool(nameof(ExceptionlessMcpTools.SearchStacksAsync));

        Assert.Contains("no stack id filter", protocolTool.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("get_stack", protocolTool.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateStackStatus_SchemaConstrainsAllowedStatuses()
    {
        var status = CreateProtocolTool(nameof(ExceptionlessMcpTools.UpdateStackStatusAsync))
            .InputSchema.GetProperty("properties").GetProperty("status");

        Assert.Equal(["open", "fixed", "ignored", "discarded"], status.GetProperty("enum").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void Serialize_DatesWithoutTimeZone_WritesUtc()
    {
        // Arrange
        var unspecified = new DateTime(2016, 1, 11, 20, 5, 59, DateTimeKind.Unspecified);
        var project = new McpProjectResult("project", "organization", "Legacy", unspecified, unspecified, "/api/v2/projects/project", LastEventDateUtc: unspecified);
        var stack = new McpStackResult("stack", "organization", "project", "error", "fixed", "Title", 1, unspecified, unspecified, [], [], false, unspecified, unspecified, "/api/v2/stacks/stack",
            DateFixed: unspecified, SnoozeUntilUtc: unspecified);
        var evt = new McpEventResult("event", "organization", "project", "stack", new DateTimeOffset(2016, 1, 11, 20, 5, 59, TimeSpan.FromHours(-5)), [], false, unspecified, "/api/v2/events/event");
        var context = new McpContextResult("organization", "Org", "project", "Legacy", [], [project], false, false, unspecified);
        var counts = new McpEventCountResult(1, 1, 1, 0, [], StartUtc: unspecified, EndUtc: unspecified, Groups: [new McpEventCountGroup("stack", 1, 1, [], FirstOccurrence: unspecified, LastOccurrence: unspecified)]);
        var contextRequired = McpResponse<McpProjectResult>.Failed(McpErrors.ContextRequired("Select a project.", "project", [], [project]));
        object[] results = [project, stack, evt, context, counts, contextRequired, McpResponse<McpListData<McpStackResult>>.Success(new McpListData<McpStackResult>([stack]))];

        // Act
        var serialized = results.Select(result => JsonSerializer.SerializeToElement(result, McpJsonSerializerOptions.Default)).ToArray();

        // Assert
        foreach (var json in serialized)
            McpDateAssert.AllDatesHaveTimeZone(json);

        Assert.Equal("2016-01-11T20:05:59Z", serialized[0].GetProperty("createdUtc").GetString());
    }

    [Fact]
    public void Serialize_LocalAndUtcDates_WritesSameInstantInUtc()
    {
        // Arrange
        var utc = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var project = new McpProjectResult("project", "organization", "Project", utc.ToLocalTime(), utc, "/api/v2/projects/project");

        // Act
        var json = JsonSerializer.SerializeToElement(project, McpJsonSerializerOptions.Default);

        // Assert
        Assert.Equal("2026-06-01T12:00:00Z", json.GetProperty("createdUtc").GetString());
        Assert.Equal("2026-06-01T12:00:00Z", json.GetProperty("updatedUtc").GetString());
    }

    [Fact]
    public void ListProjects_OutputSchemaAdvertisesDateTimeFormat()
    {
        // Arrange
        var method = typeof(ExceptionlessMcpTools).GetMethod(nameof(ExceptionlessMcpTools.ListProjectsAsync))!;

        // Act
        var protocolTool = McpServerTool.Create(method, CreateMcpTools(), new McpServerToolCreateOptions { SerializerOptions = McpJsonSerializerOptions.Default }).ProtocolTool;
        string schema = protocolTool.OutputSchema!.Value.GetRawText();

        // Assert
        Assert.Contains("\"createdUtc\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"date-time\"", schema, StringComparison.Ordinal);
    }

    private static ModelContextProtocol.Protocol.Tool CreateProtocolTool(string methodName)
    {
        var method = typeof(ExceptionlessMcpTools).GetMethod(methodName)
            ?? throw new InvalidOperationException($"Could not find {methodName}.");
        return McpServerTool.Create(method, CreateMcpTools(), new McpServerToolCreateOptions()).ProtocolTool;
    }

    private static HashSet<string> RequiredProperties(System.Text.Json.JsonElement schema)
        => schema.TryGetProperty("required", out var required)
            ? required.EnumerateArray().Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal)
            : [];

    private static ExceptionlessMcpTools CreateMcpTools() => new(
        null!,
        null!,
        null!,
        null!,
        null!,
        null!,
        null!,
        null!,
        null!,
        null!,
        null!,
        NullLogger<ExceptionlessMcpTools>.Instance,
        TimeProvider.System,
        null!);
}
