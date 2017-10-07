using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Exceptionless.Insulation.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Exceptionless.Api.Tests.Utility
{
    public class MvcWebApplicationBuilder<TStartup> where TStartup : class {
        public string ContentRoot { get; set; }
        public IList<Action<IServiceCollection>> ConfigureServicesBeforeStartup { get; set; } = new List<Action<IServiceCollection>>();
        public IList<Action<IServiceCollection>> ConfigureServicesAfterStartup { get; set; } = new List<Action<IServiceCollection>>();
        public List<Assembly> ApplicationAssemblies { get; set; } = new List<Assembly>();

        /// <summary>
        /// Configures services before TStartup.ConfigureServices runs.
        /// </summary>
        /// <param name="configure">The <see cref="Action{IServiceCollection}"/> to configure the services with.</param>
        /// <returns>An instance of this <see cref="MvcWebApplicationBuilder{TStartup}"/></returns>
        public MvcWebApplicationBuilder<TStartup> ConfigureBeforeStartup(Action<IServiceCollection> configure) {
            ConfigureServicesBeforeStartup.Add(configure);
            return this;
        }

        /// <summary>
        /// Configures services after TStartup.ConfigureServices runs.
        /// </summary>
        /// <param name="configure">The <see cref="Action{IServiceCollection}"/> to configure the services with.</param>
        /// <returns>An instance of this <see cref="MvcWebApplicationBuilder{TStartup}"/></returns>
        public MvcWebApplicationBuilder<TStartup> ConfigureAfterStartup(Action<IServiceCollection> configure) {
            ConfigureServicesAfterStartup.Add(configure);
            return this;
        }

        /// <summary>
        /// Configures <see cref="ApplicationPartManager"/> to include the default set
        /// of <see cref="ApplicationPart"/> provided by <see cref="DefaultAssemblyPartDiscoveryProvider"/>.
        /// </summary>
        /// <returns>An instance of this <see cref="MvcWebApplicationBuilder{TStartup}"/></returns>
        public MvcWebApplicationBuilder<TStartup> UseApplicationAssemblies() {
            string depsFileName = $"{typeof(TStartup).Assembly.GetName().Name}.deps.json";
            var depsFile = new FileInfo(Path.Combine(AppContext.BaseDirectory, depsFileName));
            if (!depsFile.Exists) {
                throw new InvalidOperationException($"Can't find'{depsFile.FullName}'. This file is required for functional tests " +
                    "to run properly. There should be a copy of the file on your source project bin folder. If thats not the " +
                    "case, make sure that the property PreserveCompilationContext is set to true on your project file. E.g" +
                    "'<PreserveCompilationContext>true</PreserveCompilationContext>'." +
                    $"For functional tests to work they need to either run from the build output folder or the {Path.GetFileName(depsFile.FullName)} " +
                    $"file from your application's output directory must be copied" +
                    "to the folder where the tests are running on. A common cause for this error is having shadow copying enabled when the " +
                    "tests run.");
            }

            ApplicationAssemblies.AddRange(DefaultAssemblyPartDiscoveryProvider
                .DiscoverAssemblyParts(typeof(TStartup).Assembly.GetName().Name)
                .Select(s => ((AssemblyPart)s).Assembly)
                .ToList());

            return this;
        }

        /// <summary>
        /// Configures the application content root.
        /// </summary>
        /// <param name="solutionName">The glob pattern to use for finding the solution.</param>
        /// <param name="solutionRelativePath">The relative path to the content root from the solution file.</param>
        /// <returns>An instance of this <see cref="MvcWebApplicationBuilder{TStartup}"/></returns>
        public MvcWebApplicationBuilder<TStartup> UseSolutionRelativeContentRoot(
            string solutionRelativePath,
            string solutionName = "*.sln") {
            if (solutionRelativePath == null) {
                throw new ArgumentNullException(nameof(solutionRelativePath));
            }

            string applicationBasePath = AppContext.BaseDirectory;

            var directoryInfo = new DirectoryInfo(applicationBasePath);
            do {
                string solutionPath = Directory.EnumerateFiles(directoryInfo.FullName, solutionName).FirstOrDefault();
                if (solutionPath != null) {
                    ContentRoot = Path.GetFullPath(Path.Combine(directoryInfo.FullName, solutionRelativePath));
                    return this;
                }

                directoryInfo = directoryInfo.Parent;
            }
            while (directoryInfo.Parent != null);

            throw new Exception($"Solution root could not be located using application root {applicationBasePath}.");
        }

        public TestServer Build() {
            var builder = new WebHostBuilder()
                .UseStartup<TestStartup<TStartup>>()
                // This is necessary so that IHostingEnvironment.ApplicationName has the right
                // value and libraries depending on it (to load the dependency context, for example)
                // work properly.
                .UseSetting(WebHostDefaults.ApplicationKey, typeof(TStartup).Assembly.GetName().Name)
                .UseContentRoot(ContentRoot)
                .ConfigureAppConfiguration((hostingContext, config) => {
                    config.AddYamlFile(Path.Combine(AppContext.BaseDirectory, "appsettings.yml"), optional: true, reloadOnChange: true);
                })
                .ConfigureServices(InitializeServices);

            return new TestServer(builder);
        }

        protected virtual void InitializeServices(IServiceCollection services) {
            // Inject a custom application part manager. Overrides AddMvcCore() because that uses TryAdd().
            var manager = new ApplicationPartManager();
            foreach (var assembly in ApplicationAssemblies) {
                manager.ApplicationParts.Add(new AssemblyPart(assembly));
            }

            services.AddSingleton(manager);
            services.AddSingleton(new TestServiceRegistrations {
                Before = ConfigureServicesBeforeStartup,
                After = ConfigureServicesAfterStartup
            });
        }
    }

    public class TestServiceRegistrations {
        public IList<Action<IServiceCollection>> Before { get; set; } = new List<Action<IServiceCollection>>();
        public IList<Action<IServiceCollection>> After { get; set; } = new List<Action<IServiceCollection>>();

        public void ConfigureServices(IServiceCollection services, Action startupConfigureServices) {
            foreach (var config in Before) {
                config(services);
            }

            startupConfigureServices();

            foreach (var config in After) {
                config(services);
            }
        }
    }

    public class TestStartup<TStartup> where TStartup : class {
        private readonly IServiceProvider _serviceProvider;
        private readonly TestServiceRegistrations _registrations;
        private readonly TStartup _instance;

        public TestStartup(IServiceProvider serviceProvider, TestServiceRegistrations registrations) {
            _serviceProvider = serviceProvider;
            _registrations = registrations;
            _instance = (TStartup)ActivatorUtilities.CreateInstance(serviceProvider, typeof(TStartup));
        }

        public void ConfigureServices(IServiceCollection services) {
            var configureServices = _instance.GetType().GetMethod(nameof(ConfigureServices));
            var parameters = Enumerable.Repeat(services, 1)
                .Concat(configureServices
                    .GetParameters()
                    .Skip(1)
                    .Select(p => ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, p.ParameterType)))
                .ToArray();

            _registrations.ConfigureServices(services, () => configureServices.Invoke(_instance, parameters));
        }

        public void Configure(IApplicationBuilder applicationBuilder) {
            var configure = _instance.GetType().GetMethod(nameof(Configure));
            var parameters = Enumerable.Repeat(applicationBuilder, 1)
                .Concat(configure
                    .GetParameters()
                    .Skip(1)
                    .Select(p => ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, p.ParameterType)))
                .ToArray();

            configure.Invoke(_instance, parameters);
        }
    }
}