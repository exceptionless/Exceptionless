using System;
using System.IO;
using Exceptionless.Api;
using Exceptionless.Insulation.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Web {
    public class Program {
        public static void Main(string[] args) {
            string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (String.IsNullOrWhiteSpace(environment))
                environment = "Production";

            var webHost = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureAppConfiguration((hostingContext, config) => {
                    config.AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true);
                    config.AddYamlFile($"appsettings.{environment}.yml", optional: true, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                    config.AddCommandLine(args);
                })
                .ConfigureLogging((hostingContext, logging) => {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                    logging.AddDebug();
                })
                .UseStartup<Startup>()
                .Build();

            webHost.Run();

            ExceptionlessClient.Default.ProcessQueue();
        }
    }
}
