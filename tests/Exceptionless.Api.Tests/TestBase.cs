using System;
using Exceptionless.Api.Tests.Authentication;
using Exceptionless.Api.Tests.Mail;
using Exceptionless.Core;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Mail;
using Exceptionless.Insulation.Configuration;
using Foundatio.Logging.Xunit;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests {
    public abstract class TestBase : TestWithLoggingBase, IDisposable {
        private readonly IDisposable _testSystemClock = TestSystemClock.Install();
        private IServiceProvider _container;
        private bool _initialized;

        public TestBase(ITestOutputHelper output) : base(output) {
            Log.MinimumLevel = LogLevel.Information;
            Log.SetLogLevel<ScheduledTimer>(LogLevel.Warning);
        }

        protected virtual void Initialize() {
            _container = GetDefaultContainer();
            _initialized = true;
        }

        protected virtual TService GetService<TService>() where TService : class {
            if (!_initialized)
                Initialize();

            return _container.GetRequiredService<TService>();
        }

        protected virtual void Configure(IServiceCollection serviceCollection) {
            var serviceProvider = serviceCollection.BuildServiceProvider();
            Settings.Initialize(serviceProvider.GetRequiredService<IConfiguration>(), "Development");
        }

        protected virtual void RegisterServices(IServiceCollection services) {
            services.AddSingleton<ILoggerFactory>(Log);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            Bootstrapper.RegisterServices(services, Log);

            services.AddSingleton<IMailer, NullMailer>();
            services.AddSingleton<IDomainLoginProvider, TestDomainLoginProvider>();
        }

        protected virtual IServiceProvider GetDefaultContainer() {
            var services = new ServiceCollection();

            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            Settings.Initialize(config, "Development");
            services.AddSingleton<IConfiguration>(config);
            Api.Bootstrapper.RegisterServices(services, Log);
            RegisterServices(services);

            return services.BuildServiceProvider();
        }

        public virtual void Dispose() {
            _testSystemClock.Dispose();
        }
    }
}