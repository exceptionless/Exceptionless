using System.Text.Json;
using Exceptionless.Core.Serialization;
using Exceptionless.Web.Assistant;
using Xunit;

namespace Exceptionless.Tests.Assistant;

public sealed class AssistantToolResultSerializerTests
{
    [Fact]
    public void Serialize_ProjectSetup_AddsCanonicalConfigureUrl()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureExceptionlessApiDefaults();

        string json = AssistantToolResultSerializer.Serialize(
            "get_project_setup",
            new { data = new { id = "project id", name = "Project" }, ok = true },
            options);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("/next/project/project%20id/configure", document.RootElement.GetProperty("data").GetProperty("webUrl").GetString());
    }
}
