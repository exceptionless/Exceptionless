using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests.Extensions;

public sealed class PersistentEventExtensionsTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    [Fact]
    public void UpdateSessionStart_FractionalSeconds_PreservesTickPrecision()
    {
        // Arrange
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var session = new PersistentEvent { Type = Event.KnownTypes.Session, Date = start };
        var lastActivity = start.UtcDateTime.AddTicks(1_234_567_891);

        // Act
        bool updated = session.UpdateSessionStart(lastActivity);

        // Assert
        Assert.True(updated);
        Assert.Equal(123.4567891m, session.Value);
    }
}
