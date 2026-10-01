using System.Globalization;
using Exceptionless.Web.Api.Infrastructure;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Exceptionless.Tests.Api.Infrastructure;

public sealed class TimeRangeParserTests
{
    [Fact]
    public void GetTimeInfo_CalendarRanges_ReturnsInclusiveBounds()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        var ranges = new[]
        {
            ("[2025 TO 2025]", "2025-01-01T05:00:00Z", "2026-01-01T04:59:59.999Z"),
            ("[2025-01 TO 2025-01]", "2025-01-01T05:00:00Z", "2025-02-01T04:59:59.999Z"),
            ("2025-01", "2025-01-01T05:00:00Z", "2025-02-01T04:59:59.999Z"),
            ("[2024-02 TO 2024-02]", "2024-02-01T05:00:00Z", "2024-03-01T04:59:59.999Z"),
            ("[2025-01-01 TO 2025-01-01]", "2025-01-01T05:00:00Z", "2025-01-02T04:59:59.999Z")
        };

        foreach (var (time, expectedStart, expectedEnd) in ranges)
        {
            var result = TimeRangeParser.GetTimeInfo($"date|{time}", "-5h", timeProvider, ["date"]);

            Assert.Equal("date", result.Field);
            Assert.Equal(DateTimeOffset.Parse(expectedStart, CultureInfo.InvariantCulture).UtcDateTime, result.Range.UtcStart);
            Assert.Equal(DateTimeOffset.Parse(expectedEnd, CultureInfo.InvariantCulture).UtcDateTime, result.Range.UtcEnd);
        }
    }
}
