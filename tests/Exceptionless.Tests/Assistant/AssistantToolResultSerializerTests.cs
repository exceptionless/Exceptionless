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
}
