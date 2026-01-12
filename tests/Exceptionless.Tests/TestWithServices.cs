using Exceptionless.Core;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Helpers;
using Exceptionless.Insulation.Configuration;
using Exceptionless.Tests.Authentication;
using Exceptionless.Tests.Mail;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Utility;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests;

public class TestWithServices : TestWithLoggingBase, IDisposable
{
    private readonly IServiceProvider _container;
    private readonly ProxyTimeProvider _timeProvider;

    public TestWithServices(ITestOutputHelper output) : base(output)
    {
        Log.DefaultLogLevel = LogLevel.Information;
        Log.SetLogLevel<ScheduledTimer>(LogLevel.Warning);
        Log.SetLogLevel<InMemoryMessageBus>(LogLevel.Warning);
        Log.SetLogLevel<InMemoryCacheClient>(LogLevel.Warning);

        _container = CreateContainer();

        if (GetService<TimeProvider>() is ProxyTimeProvider proxyTimeProvider)
            _timeProvider = proxyTimeProvider;
        else
            throw new InvalidOperationException("TimeProvider must be of type ProxyTimeProvider");
    }
    protected ProxyTimeProvider TimeProvider => _timeProvider;

    protected TService GetService<TService>() where TService : class
    {
        return _container.GetRequiredService<TService>();
    }

    protected virtual void RegisterServices(IServiceCollection services, AppOptions options)
    {
        services.AddSingleton<ILoggerFactory>(Log);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        Web.Bootstrapper.RegisterServices(services, options, Log);
        Bootstrapper.RegisterServices(services, options);
        services.ReplaceSingleton<TimeProvider>(_ => new ProxyTimeProvider());
        services.AddSingleton<IMailer, NullMailer>();
        services.AddSingleton<IDomainLoginProvider, TestDomainLoginProvider>();

        services.AddSingleton<EventData>();
        services.AddTransient<EventDataBuilder>();
        services.AddSingleton<OrganizationData>();
        services.AddSingleton<ProjectData>();
        services.AddSingleton<RandomEventGenerator>();
        services.AddSingleton<StackData>();
        services.AddSingleton<TokenData>();
        services.AddSingleton<UserData>();
    }

    private IServiceProvider CreateContainer()
    {
        var services = new ServiceCollection();

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddYamlFile("appsettings.yml", optional: false, reloadOnChange: false)
            .AddCustomEnvironmentVariables()
            .Build();

        services.AddSingleton<IConfiguration>(config);
        var appOptions = AppOptions.ReadFromConfiguration(config);
        services.AddSingleton(appOptions);
        RegisterServices(services, appOptions);

        return services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _timeProvider.Restore();
    }
}
