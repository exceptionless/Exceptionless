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
        TimeProvider.System);
}
