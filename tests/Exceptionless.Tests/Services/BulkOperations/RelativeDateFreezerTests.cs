using Exceptionless.Core.Services.BulkOperations;
using Xunit;

namespace Exceptionless.Tests.Services.BulkOperations;

public class RelativeDateFreezerTests
{
    private static readonly DateTime UtcNow = new(2026, 11, 3, 4, 5, 6, 789, DateTimeKind.Utc);

    [Theory]
    [InlineData("data.date:[now/d-4d TO now/d+1d}", "data.date:[\"2026-11-03T04:05:06.789Z||/d-4d\" TO \"2026-11-03T04:05:06.789Z||/d+1d\"}")]
    [InlineData("date:>now-1h", "date:>\"2026-11-03T04:05:06.789Z||-1h\"")]
    [InlineData("type:error AND date:[now-7d TO now]", "type:error AND date:[\"2026-11-03T04:05:06.789Z||-7d\" TO \"2026-11-03T04:05:06.789Z||\"]")]
    public async Task FreezeAsync_RelativeDates_AreAnchoredToTheGivenTime(string filter, string expected)
    {
        // Act
        string? frozen = await RelativeDateFreezer.FreezeAsync(filter, UtcNow);

        // Assert
        Assert.Equal(expected, frozen);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("type:error")]
    [InlineData("message:now")]
    [InlineData("date:[2026-01-01 TO 2026-02-01]")]
    [InlineData("tag:[nowhere TO zebra]")]
    public async Task FreezeAsync_WithoutRelativeDates_LeavesTheFilterAlone(string? filter)
    {
        // Act
        string? frozen = await RelativeDateFreezer.FreezeAsync(filter, UtcNow);

        // Assert
        Assert.DoesNotContain("||", frozen ?? String.Empty);
    }
}
