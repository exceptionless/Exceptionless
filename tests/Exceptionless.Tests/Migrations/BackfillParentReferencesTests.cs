using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Tasks;
using Exceptionless.Core.Migrations;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public sealed class BackfillParentReferencesTests
{
    [Fact]
    public void EnsureTaskSucceeded_WithTaskError_Throws()
    {
        var taskStatus = new GetTasksResponse
        {
            Completed = true,
            Error = new ErrorCause("script_exception") { Reason = "parent script failed" },
            Task = null!
        };

        var exception = Assert.Throws<ApplicationException>(() => BackfillParentReferences.EnsureTaskSucceeded(taskStatus));

        Assert.Contains("parent script failed", exception.Message);
    }

    [Fact]
    public void EnsureTaskSucceeded_WithEmbeddedFailures_Throws()
    {
        using var response = JsonDocument.Parse("""{"failures":[{"cause":{"reason":"mapping failure"}}]}""");
        var taskStatus = new GetTasksResponse
        {
            Completed = true,
            Response = response.RootElement.Clone(),
            Task = null!
        };

        var exception = Assert.Throws<ApplicationException>(() => BackfillParentReferences.EnsureTaskSucceeded(taskStatus));

        Assert.Contains("mapping failure", exception.Message);
    }

    [Fact]
    public void EnsureTaskSucceeded_WithVersionConflicts_Throws()
    {
        using var response = JsonDocument.Parse("""{"version_conflicts":2,"failures":[]}""");
        var taskStatus = new GetTasksResponse
        {
            Completed = true,
            Response = response.RootElement.Clone(),
            Task = null!
        };

        var exception = Assert.Throws<ApplicationException>(() => BackfillParentReferences.EnsureTaskSucceeded(taskStatus));

        Assert.Contains("2 version conflicts", exception.Message);
    }

    [Fact]
    public void EnsureTaskSucceeded_WithoutFailures_DoesNotThrow()
    {
        using var response = JsonDocument.Parse("""{"failures":[]}""");
        var taskStatus = new GetTasksResponse
        {
            Completed = true,
            Response = response.RootElement.Clone(),
            Task = null!
        };

        BackfillParentReferences.EnsureTaskSucceeded(taskStatus);
    }
}
