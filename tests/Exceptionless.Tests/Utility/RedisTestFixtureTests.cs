using Aspire.Hosting.ApplicationModel;
using Xunit;

namespace Exceptionless.Tests.Utility;

public sealed class RedisTestFixtureTests
{
    [Fact]
    public async Task CreateBuilderAsync_RegistersOnlyAnIsolatedRedisContainer()
    {
        await using var builder = await RedisTestFixture.CreateBuilderAsync(TestContext.Current.CancellationToken);

        var resource = Assert.IsType<RedisResource>(Assert.Single(builder.Resources.OfType<ContainerResource>()));
        Assert.Equal("Redis", resource.Name);
        Assert.Null(Assert.Single(resource.Annotations.OfType<EndpointAnnotation>()).Port);
        Assert.DoesNotContain(resource.Annotations, annotation => annotation is ContainerLifetimeAnnotation { Lifetime: ContainerLifetime.Persistent });
    }
}
