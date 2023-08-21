﻿using Exceptionless.Core;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Mail;
using Exceptionless.Insulation.Configuration;
using Exceptionless.Tests.Authentication;
using Exceptionless.Tests.Mail;
using Foundatio.Caching;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Messaging;
using Foundatio.Metrics;
using Foundatio.Utility;
using Foundatio.Xunit;
using Xunit.Abstractions;
using IAsyncLifetime = Foundatio.Utility.IAsyncLifetime;

namespace Exceptionless.Tests;

public class TestWithServices : TestWithLoggingBase, IAsyncLifetime
{
    private readonly IDisposable _testSystemClock = TestSystemClock.Install();
    private readonly IServiceProvider _container;

    public TestWithServices(ITestOutputHelper output) : base(output)
    {
        Log.MinimumLevel = LogLevel.Information;
        Log.SetLogLevel<ScheduledTimer>(LogLevel.Warning);
        Log.SetLogLevel<InMemoryMessageBus>(LogLevel.Warning);
        Log.SetLogLevel<InMemoryCacheClient>(LogLevel.Warning);
        Log.SetLogLevel<InMemoryMetricsClient>(LogLevel.Information);

        _container = CreateContainer();
    }

    public virtual async Task InitializeAsync()
    {
        var result = await _container.RunStartupActionsAsync();
        if (!result.Success)
            throw new ApplicationException($"Startup action \"{result.FailedActionName}\" failed");
    }

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
        services.AddSingleton<IMailer, NullMailer>();
        services.AddSingleton<IDomainLoginProvider, TestDomainLoginProvider>();
    }

    private IServiceProvider CreateContainer()
    {
        var services = new ServiceCollection();

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddYamlFile("appsettings.yml", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        services.AddSingleton<IConfiguration>(config);
        var appOptions = AppOptions.ReadFromConfiguration(config);
        services.AddSingleton(appOptions);
        RegisterServices(services, appOptions);

        return services.BuildServiceProvider();
    }

    public ValueTask DisposeAsync()
    {
        _testSystemClock.Dispose();
        return new ValueTask(Task.CompletedTask);
    }
}
