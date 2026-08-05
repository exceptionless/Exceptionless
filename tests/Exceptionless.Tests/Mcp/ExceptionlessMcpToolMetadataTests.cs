using System.Runtime.CompilerServices;
using Exceptionless.Web.Mcp;
using ModelContextProtocol.Server;
using Xunit;

namespace Exceptionless.Tests.Mcp;

public sealed class ExceptionlessMcpToolMetadataTests
{
    [Theory]
    [InlineData(nameof(ExceptionlessMcpTools.ListOrganizationsAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.ResolveProjectAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.ListProjectsAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.GetProjectAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.GetClientSetupInstructionsAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.SearchStacksAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.GetStackAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.GetStackEventsAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.SearchEventsAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.GetEventAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.CountEventsAsync))]
    [InlineData(nameof(ExceptionlessMcpTools.GetFilterFields))]
    public void ReadTools_AnnotationsAdvertiseReadOnly(string methodName)
    {
        var tool = CreateTool(methodName);

        Assert.NotNull(tool.ProtocolTool.Annotations);
        Assert.Equal(true, tool.ProtocolTool.Annotations!.ReadOnlyHint);
    }

    [Theory]
    [InlineData(nameof(ExceptionlessMcpTools.UpdateStackStatusAsync), true, true)]
    [InlineData(nameof(ExceptionlessMcpTools.SnoozeStackAsync), true, false)]
    [InlineData(nameof(ExceptionlessMcpTools.SetStackCriticalAsync), true, true)]
    [InlineData(nameof(ExceptionlessMcpTools.AddStackReferenceLinkAsync), false, true)]
    [InlineData(nameof(ExceptionlessMcpTools.RemoveStackReferenceLinkAsync), true, true)]
    public void StackWriteTools_AnnotationsAdvertiseSideEffects(string methodName, bool destructive, bool idempotent)
    {
        var tool = CreateTool(methodName);

        Assert.NotNull(tool.ProtocolTool.Annotations);
        Assert.Equal(false, tool.ProtocolTool.Annotations!.ReadOnlyHint);
        Assert.Equal(destructive, tool.ProtocolTool.Annotations.DestructiveHint);
        Assert.Equal(idempotent, tool.ProtocolTool.Annotations.IdempotentHint);
    }

    private static McpServerTool CreateTool(string methodName)
    {
        var method = typeof(ExceptionlessMcpTools).GetMethod(methodName) ?? throw new InvalidOperationException($"Could not find {methodName}.");
        var target = (ExceptionlessMcpTools)RuntimeHelpers.GetUninitializedObject(typeof(ExceptionlessMcpTools));
        return McpServerTool.Create(method, target, new McpServerToolCreateOptions());
    }
}
