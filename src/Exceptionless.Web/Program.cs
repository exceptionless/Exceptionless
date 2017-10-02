using System;
using System.IO;
using System.Net;
using Exceptionless.Api;
using Exceptionless.Insulation.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Web {
    public class Program {
        public static void Main(string[] args) {
            var webHost = new WebHostBuilder()
                .UseKestrel(o => {
                    o.Listen(IPAddress.Loopback, 5000);

                    string certPath = Path.Combine(Directory.GetCurrentDirectory(), "dev.pfx");
                    if (File.Exists(certPath)) {
                        o.Listen(IPAddress.Loopback, 5100, listenOptions => {
                            listenOptions.UseHttps(certPath, "contacts");
                            listenOptions.UseConnectionLogging();
                        });
                    }

                    o.UseSystemd();
                })
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureAppConfiguration((hostingContext, config) => {
                    var env = hostingContext.HostingEnvironment;
                    config.AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true);
                    config.AddYamlFile($"appsettings.{env.EnvironmentName}.yml", optional: true, reloadOnChange: true);
                    config.AddYamlFile("../../../../../appsettings.yml", optional: true, reloadOnChange: true);
                    config.AddYamlFile("../../../appsettings.yml", optional: true, reloadOnChange: true);
                    config.AddYamlFile("../../appsettings.yml", optional: true, reloadOnChange: true);
                    config.AddYamlFile("../appsettings.yml", optional: true, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                    config.AddCommandLine(args);
                })
                .UseIISIntegration()
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
