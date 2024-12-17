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
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Messaging;
using Foundatio.Utility;
using Foundatio.Xunit;
using Xunit.Abstractions;
using IAsyncLifetime = Xunit.IAsyncLifetime;

namespace Exceptionless.Tests;

public class TestWithServices : TestWithLoggingBase, IAsyncLifetime
{
    private readonly IServiceProvider _container;
    private readonly ProxyTimeProvider _timeProvider;
    //private static bool _startupActionsRun;

    public TestWithServices(ITestOutputHelper output) : base(output)
    {
        Log.DefaultMinimumLevel = LogLevel.Information;
        Log.SetLogLevel<ScheduledTimer>(LogLevel.Warning);
        Log.SetLogLevel<InMemoryMessageBus>(LogLevel.Warning);
        Log.SetLogLevel<InMemoryCacheClient>(LogLevel.Warning);

        _container = CreateContainer();

        if (GetService<TimeProvider>() is ProxyTimeProvider proxyTimeProvider)
            _timeProvider = proxyTimeProvider;
        else
            throw new InvalidOperationException("TimeProvider must be of type ProxyTimeProvider");
    }

    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;

        /*
        if (_startupActionsRun)
            return;

        var result = await _container.RunStartupActionsAsync();
        if (!result.Success)
            throw new ApplicationException($"Startup action \"{result.FailedActionName}\" failed");

        _startupActionsRun = true;*/
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

    public Task DisposeAsync()
    {
        _timeProvider.Restore();
        return Task.CompletedTask;
    }
}
