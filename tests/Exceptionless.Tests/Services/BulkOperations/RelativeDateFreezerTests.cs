using Exceptionless.Core.Services.BulkOperations;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests.Services.BulkOperations;

public class RelativeDateFreezerTests : TestWithLoggingBase
{
    private static readonly DateTime UtcNow = new(2026, 11, 3, 4, 5, 6, 789, DateTimeKind.Utc);

    public RelativeDateFreezerTests(ITestOutputHelper output) : base(output) { }

    // Built-in fields are looked up in the index mapping; here only date and created_utc are dates.
    private static ValueTask<bool> IsDateFieldAsync(string field) => ValueTask.FromResult(field is "date" or "created_utc");

    [Theory]
    [InlineData("date:>now-1h", "date:>\"2026-11-03T04:05:06.789Z||-1h\"")]
    [InlineData("type:error AND date:[now-7d TO now]", "type:error AND date:[\"2026-11-03T04:05:06.789Z||-7d\" TO \"2026-11-03T04:05:06.789Z||\"]")]
    [InlineData("date:(>now-2h AND <=now)", "date:(>\"2026-11-03T04:05:06.789Z||-2h\" AND <=\"2026-11-03T04:05:06.789Z||\")")]
    [InlineData("data.date:(>=now/d AND <now/d+1d)", "idx.date-d:(>=\"2026-11-03T04:05:06.789Z||/d\" AND <\"2026-11-03T04:05:06.789Z||/d+1d\")")]
    [InlineData("data.date:[now/d-4d TO now/d+1d}", "idx.date-d:[\"2026-11-03T04:05:06.789Z||/d-4d\" TO \"2026-11-03T04:05:06.789Z||/d+1d\"}")]
    public async Task FreezeAsync_RelativeDatesOnDateFields_AreAnchoredToTheGivenTime(string filter, string expected)
    {
        // Act
        string? frozen = await RelativeDateFreezer.FreezeAsync(filter, UtcNow, IsDateFieldAsync);

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
    [InlineData("environment:[now TO z]")]
    [InlineData("environment:(>now AND <z)")]
    // The events index stores this group as a string field, because its first term is not a date.
    [InlineData("data.date:(>now-2h AND <=now)")]
    [InlineData("data.name:[now TO z]")]
    [InlineData("ref.parent:[now TO z]")]
    public async Task FreezeAsync_WithoutRelativeDatesOnDateFields_LeavesTheFilterAlone(string? filter)
    {
        // Act
        string? frozen = await RelativeDateFreezer.FreezeAsync(filter, UtcNow, IsDateFieldAsync);

        // Assert
        Assert.DoesNotContain("||", frozen ?? String.Empty);
    }
}
