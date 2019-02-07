using System;
using System.Threading;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Exceptionless.Job {
    public class Startup {
        public Startup(ILoggerFactory loggerFactory) {
            LoggerFactory = loggerFactory;
        }

        public ILoggerFactory LoggerFactory { get; }

        public void ConfigureServices(IServiceCollection services) {
            services.Configure<ForwardedHeadersOptions>(o => {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                o.RequireHeaderSymmetry = false;
            });
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
              .AddJsonOptions(o => {
                o.SerializerSettings.DefaultValueHandling = DefaultValueHandling.Include;
                o.SerializerSettings.NullValueHandling = NullValueHandling.Include;
                o.SerializerSettings.Formatting = Formatting.Indented;
                o.SerializerSettings.ContractResolver = Core.Bootstrapper.GetJsonContractResolver(); // TODO: See if we can resolve this from the di.
            });

            services.AddRouting(r => r.LowercaseUrls = true);
                        
            Core.Bootstrapper.RegisterServices(services);
            var serviceProvider = services.BuildServiceProvider();
            var options = serviceProvider.GetRequiredService<IOptions<AppOptions>>().Value;
            Insulation.Bootstrapper.RegisterServices(serviceProvider, services, options, options.RunJobsInProcess);
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env) {
            var settings = app.ApplicationServices.GetRequiredService<IOptions<AppOptions>>().Value;
            Core.Bootstrapper.LogConfiguration(app.ApplicationServices, settings, LoggerFactory);

            if (settings.EnableHealthChecks)
                app.UseHealthChecks("/health", new HealthCheckOptions {
                    Predicate = hcr => hcr.Tags.Contains("Core") || hcr.Tags.Contains(Program.JobName)
                });

            if (!String.IsNullOrEmpty(settings.ExceptionlessApiKey) && !String.IsNullOrEmpty(settings.ExceptionlessServerUrl))
                app.UseExceptionless(ExceptionlessClient.Default);

            app.UseHttpMethodOverride();
            app.UseForwardedHeaders();
            app.UseMvc();

            // run startup actions registered in the container
            if (settings.EnableBootstrapStartupActions) {
                var lifetime = app.ApplicationServices.GetRequiredService<IApplicationLifetime>();
                lifetime.ApplicationStarted.Register(() => {
                    var shutdownSource = new CancellationTokenSource();
                    Console.CancelKeyPress += (sender, args) => {
                        shutdownSource.Cancel();
                        args.Cancel = true;
                    };

                    var combined = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping, shutdownSource.Token);
                    app.ApplicationServices.RunStartupActionsAsync(combined.Token).GetAwaiter().GetResult();
                });
            }
        }
    }
}