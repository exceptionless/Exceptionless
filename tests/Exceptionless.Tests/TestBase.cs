using System;
using Exceptionless.Tests.Authentication;
using Exceptionless.Tests.Mail;
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

namespace Exceptionless.Tests {
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

        protected virtual void Configure(IServiceCollection services) {
            services.AddSingleton(ReadSettings());
            _container = services.BuildServiceProvider();
            _initialized = true;
        }

        protected virtual Settings ReadSettings() {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddYamlFile("appsettings.yml", optional: true)
                .AddEnvironmentVariables()
                .Build();

            return Settings.ReadFromConfiguration(config, "Development");
        }

        protected virtual void RegisterServices(IServiceCollection services) {
            services.AddSingleton<ILoggerFactory>(Log);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            Web.Bootstrapper.RegisterServices(services, Log);

            services.AddSingleton<IMailer, NullMailer>();
            services.AddSingleton<IDomainLoginProvider, TestDomainLoginProvider>();
        }

        protected virtual IServiceProvider GetDefaultContainer() {
            var container = new ServiceCollection();
            Configure(container);
            RegisterServices(container);
            return container.BuildServiceProvider();
        }

        public virtual void Dispose() {
            _testSystemClock.Dispose();
        }
    }
}