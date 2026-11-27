using Elastic.Clients.Elasticsearch;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories.Exceptions;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests.Repositories;

public sealed class EventRepositoryPartialResultTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    [Fact]
    public void EnsureComplete_WhenAllShardsSucceeded_DoesNotThrow()
    {
        // Arrange
        var shards = new ShardStatistics { Total = 2, Successful = 2, Failed = 0 };

        // Act
        var exception = Record.Exception(() => EventRepository.EnsureComplete(shards, false, "paging stack ids"));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void EnsureComplete_WhenAShardFailed_Throws()
    {
        // Arrange
        var shards = new ShardStatistics { Total = 2, Successful = 1, Failed = 1 };

        // Act
        var exception = Record.Exception(() => EventRepository.EnsureComplete(shards, false, "paging stack ids"));

        // Assert
        Assert.IsType<DocumentException>(exception);
    }

    [Fact]
    public void EnsureComplete_WhenTheSearchTimedOut_Throws()
    {
        // Arrange
        var shards = new ShardStatistics { Total = 1, Successful = 1, Failed = 0 };

        // Act
        var exception = Record.Exception(() => EventRepository.EnsureComplete(shards, true, "paging event ids"));

        // Assert
        Assert.IsType<DocumentException>(exception);
    }
}
