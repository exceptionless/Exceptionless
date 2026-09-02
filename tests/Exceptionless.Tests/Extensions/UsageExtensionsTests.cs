using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Xunit;

namespace Exceptionless.Tests.Extensions;

public class UsageExtensionsTests
{
    [Fact]
    public void MaterializeMonthlyUsage_ReturnsNewHistoryWithoutMutatingSource()
    {
        // Arrange
        var marchUsage = new UsageInfo { Date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 3_000 };
        var mayUsage = new UsageInfo { Date = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), Limit = 15_000 };
        ICollection<UsageInfo> source = [marchUsage, mayUsage];

        // Act
        var result = source.MaterializeMonthlyUsage(
            new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            75_000);

        // Assert
        Assert.Equal(2, source.Count);
        Assert.Same(marchUsage, source.Single(usage => usage.Date.Month == 3));
        Assert.NotSame(marchUsage, result.Single(usage => usage.Date.Month == 3));
        Assert.Equal(3_000, result.Single(usage => usage.Date.Month == 2).Limit);
        Assert.Equal(3_000, result.Single(usage => usage.Date.Month == 4).Limit);
        Assert.Equal(15_000, result.Single(usage => usage.Date.Month == 5).Limit);
    }
}
