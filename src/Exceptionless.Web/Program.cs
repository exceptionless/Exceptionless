using System.Diagnostics;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Insulation.Configuration;
using OpenTelemetry;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Exceptionless;

namespace Exceptionless.Web;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            await CreateHostBuilder(args).Build().RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Job host terminated unexpectedly");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
            await ExceptionlessClient.Default.ProcessQueueAsync();

            if (Debugger.IsAttached)
                Console.ReadKey();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        string? environment = Environment.GetEnvironmentVariable("EX_AppMode");
        if (String.IsNullOrWhiteSpace(environment))
            environment = "Production";

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
            .AddYamlFile($"appsettings.{environment}.yml", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables("EX_")
            .AddEnvironmentVariables("ASPNETCORE_")
            .AddCommandLine(args)
            .Build();

        return CreateHostBuilder(config, environment);
    }

    public static IHostBuilder CreateHostBuilder(IConfigurationRoot config, string environment)
    {
        Console.Title = "Exceptionless Web";

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(config)
            .CreateBootstrapLogger()
            .ForContext<Program>();

        var options = AppOptions.ReadFromConfiguration(config);
        var apmConfig = new ApmConfig(config, "web", options.InformationalVersion, options.CacheOptions.Provider == "redis");

        var configDictionary = config.ToDictionary("Serilog");
        Log.Information("Bootstrapping Exceptionless Web in {AppMode} mode ({InformationalVersion}) on {MachineName} with settings {@Settings}", environment, options.InformationalVersion, Environment.MachineName, configDictionary);

        var builder = Host.CreateDefaultBuilder()
            .UseEnvironment(environment)
            .ConfigureLogging(b => b.ClearProviders()) // clears .net providers since we are telling serilog to write to providers we only want it to be the otel provider
            .UseSerilog((ctx, sp, c) =>
            {
                c.ReadFrom.Configuration(ctx.Configuration);
                c.ReadFrom.Services(sp);
                c.Enrich.WithMachineName();

                if (!String.IsNullOrEmpty(options.ExceptionlessApiKey))
                    c.WriteTo.Sink(new ExceptionlessSink(), LogEventLevel.Information);
            }, writeToProviders: true)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder
                    .UseConfiguration(config)
                    .ConfigureKestrel(c =>
                    {
                        c.AddServerHeader = false;

                        if (options.MaximumEventPostSize > 0)
                            c.Limits.MaxRequestBodySize = options.MaximumEventPostSize;
                    })
                    .UseStartup<Startup>();
            })
            .ConfigureServices((ctx, services) =>
            {
                services.AddSingleton(config);
                services.AddSingleton(apmConfig);
                services.AddAppOptions(options);
                services.AddHttpContextAccessor();
            })
            .AddApm(apmConfig);

        return builder;
    }
}
