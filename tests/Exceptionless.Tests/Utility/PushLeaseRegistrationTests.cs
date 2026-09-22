using Exceptionless.Core;
using Exceptionless.Core.Utility;
using Exceptionless.Insulation.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Exceptionless.Tests.Utility;

public sealed class PushLeaseRegistrationTests
{
    [Fact]
    public void RedisMessageBusWithoutDistributedCache_UsesDistributedPushLeases()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BaseURL"] = "http://localhost:7110",
            ["ConnectionStrings:Cache"] = "provider=memory;",
            ["ConnectionStrings:MessageBus"] = "provider=redis;server=localhost:6379;"
        }).Build();
        var options = AppOptions.ReadFromConfiguration(configuration);
        var services = new ServiceCollection();
        services.AddSingleton<IConnectionLeaseStore, ConnectionLeaseStore>();

        Insulation.Bootstrapper.RegisterServices(services, options, runMaintenanceTasks: false);

        Assert.Equal(typeof(RedisConnectionLeaseStore), Assert.Single(services, service => service.ServiceType == typeof(IConnectionLeaseStore)).ImplementationType);
    }
}
