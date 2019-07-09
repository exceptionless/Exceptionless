using System;
using System.Threading.Tasks;
using Exceptionless.Core.Authentication;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Insulation.Configuration;
using Exceptionless.Tests.Authentication;
using Exceptionless.Tests.Mail;
using Foundatio.Hosting.Startup;
using Foundatio.Logging.Xunit;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using IAsyncLifetime = Xunit.IAsyncLifetime;

namespace Exceptionless.Tests {
    public class ElasticTestBase : TestWithLoggingBase, IAsyncLifetime {
        private readonly IDisposable _testSystemClock = TestSystemClock.Install();
        protected readonly ExceptionlessElasticConfiguration _configuration;
        private readonly IServiceProvider _container;

        public ElasticTestBase(ITestOutputHelper output) : base(output) {
            Log.MinimumLevel = LogLevel.Information;
            Log.SetLogLevel<ScheduledTimer>(LogLevel.Warning);
            _container = CreateContainer();
            _configuration = GetService<ExceptionlessElasticConfiguration>();
        }

        public virtual async Task InitializeAsync() {
            var result = await _container.RunStartupActionsAsync();
            if (!result.Success)
                throw new ApplicationException($"Startup action \"{result.FailedActionName}\" failed");
        }

        protected TService GetService<TService>() where TService : class {
            return _container.GetRequiredService<TService>();
        }

        protected virtual void RegisterServices(IServiceCollection services) {
            services.AddSingleton<ILoggerFactory>(Log);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            Web.Bootstrapper.RegisterServices(services, Log);

            services.AddSingleton<IMailer, NullMailer>();
            services.AddSingleton<IDomainLoginProvider, TestDomainLoginProvider>();
            services.AddStartupAction("Configure indexes", ConfigureIndexes, 0);
        }

        private IServiceProvider CreateContainer() {
            var services = new ServiceCollection();
            
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            
            services.AddSingleton<IConfiguration>(config);
            RegisterServices(services);

            return services.BuildServiceProvider();
        }

        private async Task ConfigureIndexes(IServiceProvider serviceProvider) {
            var configuration = serviceProvider.GetRequiredService<ExceptionlessElasticConfiguration>();
            await configuration.DeleteIndexesAsync();
            await configuration.ConfigureIndexesAsync(beginReindexingOutdated: false);
        }

        public Task DisposeAsync() {
            _configuration?.Dispose();
            _testSystemClock.Dispose();
            return Task.CompletedTask;
        }
    }
}