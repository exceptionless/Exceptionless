using Exceptionless.Core.Jobs;
using Xunit;
using BulkIndexByScrollFailure = Elastic.Clients.Elasticsearch.BulkIndexByScrollFailure;
using DeleteByQueryResponse = Elastic.Clients.Elasticsearch.DeleteByQueryResponse;
using ErrorCause = Elastic.Clients.Elasticsearch.ErrorCause;
using SearchResponse = Elastic.Clients.Elasticsearch.SearchResponse<Exceptionless.Core.Models.PersistentEvent>;
using ShardStatistics = Elastic.Clients.Elasticsearch.ShardStatistics;

namespace Exceptionless.Tests.Jobs;

public class CleanupOrphanedDataResponseTests
{
    [Fact]
    public void EnsureValidDeleteResponse_UnrecoverableFailure_Throws()
    {
        // Arrange
        var response = new DeleteByQueryResponse
        {
            Failures = [
                new BulkIndexByScrollFailure(
                    new ErrorCause("search_phase_execution_exception") { Reason = "shard failed" },
                    "event-id",
                    "events-v1-2026.07.30",
                    500)
            ]
        };

        // Act
        var exception = Assert.Throws<ApplicationException>(() =>
            CleanupOrphanedDataJob.EnsureValidDeleteResponse(response, "deleting orphaned events"));

        // Assert
        Assert.Equal("Error deleting orphaned events: Elasticsearch reported 1 unrecoverable delete failures.", exception.Message);
    }

    [Fact]
    public void EnsureValidDeleteResponse_Timeout_Throws()
    {
        // Arrange
        var response = new DeleteByQueryResponse { TimedOut = true };

        // Act
        var exception = Assert.Throws<ApplicationException>(() =>
            CleanupOrphanedDataJob.EnsureValidDeleteResponse(response, "deleting orphaned events"));

        // Assert
        Assert.Equal("Error deleting orphaned events: Elasticsearch timed out before completing the delete.", exception.Message);
    }

    [Fact]
    public void EnsureValidSearchResponse_Timeout_Throws()
    {
        // Arrange
        var response = new SearchResponse { TimedOut = true };

        // Act
        var exception = Assert.Throws<ApplicationException>(() =>
            CleanupOrphanedDataJob.EnsureValidSearchResponse(response, "getting orphaned events"));

        // Assert
        Assert.Equal("Error getting orphaned events: Elasticsearch timed out before completing the search.", exception.Message);
    }

    [Fact]
    public void EnsureValidSearchResponse_FailedShard_Throws()
    {
        // Arrange
        var response = new SearchResponse
        {
            Shards = new ShardStatistics
            {
                Failed = 1,
                Successful = 1,
                Total = 2
            }
        };

        // Act
        var exception = Assert.Throws<ApplicationException>(() =>
            CleanupOrphanedDataJob.EnsureValidSearchResponse(response, "getting orphaned events"));

        // Assert
        Assert.Equal("Error getting orphaned events: Elasticsearch reported 1 failed search shards.", exception.Message);
    }
}
