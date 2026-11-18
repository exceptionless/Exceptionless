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

    [Theory]
    [InlineData("stacks", "/stack/production-errors")]
    [InlineData("events", "/event/production-errors")]
    [InlineData("sessions", "/sessions/production-errors")]
    [InlineData("stream", "/stream?saved=view-id")]
    public void Serialize_SavedViewResult_LinksToItsPage(string viewType, string expectedUrl)
    {
        // Arrange
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureExceptionlessApiDefaults();
        var view = new McpSavedViewResult("view-id", "organization-id", "Production errors", "production-errors", viewType, IsPrivate: true, IsPredefined: false, "/api/v2/saved-views/view-id");

        // Act
        string created = AssistantToolResultSerializer.Serialize("create_saved_view", McpResponse<McpSavedViewResult>.Success(view), options);
        string listed = AssistantToolResultSerializer.Serialize("list_saved_views", McpResponse<McpListData<McpSavedViewResult>>.Success(new McpListData<McpSavedViewResult>([view])), options);
        string deleted = AssistantToolResultSerializer.Serialize("delete_saved_view", McpResponse<McpSavedViewResult>.Success(view), options);

        // Assert
        using var createdDocument = JsonDocument.Parse(created);
        using var listedDocument = JsonDocument.Parse(listed);
        using var deletedDocument = JsonDocument.Parse(deleted);
        Assert.Equal(expectedUrl, createdDocument.RootElement.GetProperty("data").GetProperty("webUrl").GetString());
        Assert.Equal(expectedUrl, listedDocument.RootElement.GetProperty("data").GetProperty("items")[0].GetProperty("webUrl").GetString());
        Assert.False(deletedDocument.RootElement.GetProperty("data").TryGetProperty("webUrl", out _));
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
