using Exceptionless.Tests.Utility;
using Exceptionless.Web.Models;
using Xunit;

namespace Exceptionless.Tests.Extensions;

public class ViewProjectExtensionsTests
{
    private readonly ProxyTimeProvider _timeProvider = new();

    [Fact]
    public void EnsureUsage_YoungOrganization_DoesNotCreateUsageBeforeProject()
    {
        // Arrange
        _timeProvider.SetUtcNow(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var organizationCreatedUtc = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var project = new ViewProject
        {
            CreatedUtc = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        project.EnsureUsage(15_000, organizationCreatedUtc, _timeProvider);

        // Assert
        Assert.Equal(2, project.Usage.Count);
        Assert.DoesNotContain(project.Usage, usage => usage.Date < new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}
