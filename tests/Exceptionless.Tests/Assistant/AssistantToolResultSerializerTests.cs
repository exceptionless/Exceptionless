using System.Text.Json;
using Exceptionless.Core.Serialization;
using Exceptionless.Web.Assistant;
using Exceptionless.Web.Mcp;
using Xunit;

namespace Exceptionless.Tests.Assistant;

public sealed class AssistantToolResultSerializerTests
{
    [Fact]
    public void Serialize_ProjectSetupResult_PreservesCanonicalConfigureUrl()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureExceptionlessApiDefaults();

        string json = AssistantToolResultSerializer.Serialize(
            "get_project_setup",
            McpResponse<McpProjectSetupResult>.Success(new McpProjectSetupResult(
                "project id",
                "Project",
                AssistantRoutes.ProjectConfigure("project id"),
                [],
                [])),
            options);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(AssistantRoutes.ProjectConfigure("project id"), document.RootElement.GetProperty("data").GetProperty("webUrl").GetString());
    }

    [Fact]
    public void Serialize_CountEventsGroupedByStack_LinksEachStack()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureExceptionlessApiDefaults();

        string json = AssistantToolResultSerializer.Serialize(
            "count_events",
            McpResponse<McpEventCountResult>.Success(new McpEventCountResult(4, 13, 2, 0, [], GroupBy: "stack", Groups:
            [
                new McpEventCountGroup("stack-a", 1, 10, [], "Checkout failed", "open"),
                new McpEventCountGroup("stack-b", 3, 3, [], "Timeout", "open")
            ])),
            options);

        using var document = JsonDocument.Parse(json);
        var groups = document.RootElement.GetProperty("data").GetProperty("groups");
        Assert.Equal(AssistantRoutes.Stack("stack-a"), groups[0].GetProperty("webUrl").GetString());
        Assert.Equal(AssistantRoutes.Stack("stack-b"), groups[1].GetProperty("webUrl").GetString());
    }

    [Fact]
    public void Serialize_CountEventsGroupedByVersion_AddsNoLinks()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureExceptionlessApiDefaults();

        string json = AssistantToolResultSerializer.Serialize(
            "count_events",
            McpResponse<McpEventCountResult>.Success(new McpEventCountResult(1, 1, 1, 0, [], GroupBy: "version", Groups: [new McpEventCountGroup("1.0.0", 1, 1, [])])),
            options);

        Assert.DoesNotContain("webUrl", json);
    }
}
