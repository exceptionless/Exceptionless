using System;
using Exceptionless.Api.Tests.Authentication;
using Exceptionless.Api.Tests.Mail;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Mail;
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

        protected virtual void RegisterServices(IServiceCollection services) {
            Bootstrapper.RegisterServices(services, Log);

            services.AddSingleton<IMailer, NullMailer>();
            services.AddSingleton<IDomainLoginProvider, TestDomainLoginProvider>();
        }

        protected virtual IServiceProvider GetDefaultContainer() {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            Api.Bootstrapper.RegisterServices(services, Log);
            RegisterServices(services);

            return services.BuildServiceProvider();
        }

        public virtual void Dispose() {
            _testSystemClock.Dispose();
        }
    }
}