using System;
using Exceptionless.Tests.Authentication;
using Exceptionless.Tests.Mail;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Mail;
using Exceptionless.Insulation.Configuration;
using Foundatio.Logging.Xunit;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using System.Collections.Generic;
using Xunit;

namespace Exceptionless.Tests {
    public class ServicesFixture : IDisposable {
        private readonly IDisposable _testSystemClock = TestSystemClock.Install();
        private readonly Lazy<IServiceProvider> _serviceProvider;
        private readonly List<Action<IServiceCollection>> _serviceConfigurations = new List<Action<IServiceCollection>>();

        public ServicesFixture() {
            _serviceProvider = new Lazy<IServiceProvider>(GetServiceProvider);
        }

        private IServiceProvider GetServiceProvider() {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(GetConfiguration());

            foreach (var configurator in _serviceConfigurations)
                configurator(services);

            return services.BuildServiceProvider();
        }

        public IServiceProvider Services => _serviceProvider.Value;

        public void AddServicesConfiguration(Action<IServiceCollection> configuration) {
            _serviceConfigurations.Add(configuration);
        }

        protected virtual IConfigurationRoot GetConfiguration() {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            return config;
        }

        public virtual void Dispose() {
            _testSystemClock.Dispose();
        }
    }

    public class TestWithServices : TestWithLoggingBase, IClassFixture<ServicesFixture> {
        private readonly ServicesFixture _fixture;

        public TestWithServices(ServicesFixture fixture, ITestOutputHelper output) : base(output) {
            _fixture = fixture;
            Log.MinimumLevel = LogLevel.Information;
            Log.SetLogLevel<ScheduledTimer>(LogLevel.Warning);

            _fixture.AddServicesConfiguration(s => {
                s.AddSingleton<ILoggerFactory>(Log);
                s.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
                Web.Bootstrapper.RegisterServices(s, Log);
                s.AddSingleton<IMailer, NullMailer>();
                s.AddSingleton<IDomainLoginProvider, TestDomainLoginProvider>();
            });
        }

        protected virtual TService GetService<TService>() => _fixture.Services.GetRequiredService<TService>();
    }
}