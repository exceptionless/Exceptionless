using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Exceptionless.Insulation.Configuration;
using Exceptionless.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Exceptionless.Tests;

public class AppWebHostFactory : WebApplicationFactory<Startup>, IAsyncLifetime
{
    private DistributedApplication? _app;

    public DistributedApplication App => _app ?? throw new InvalidOperationException("The application is not initialized");

    public Task InitializeAsync()
    {
        var options = new DistributedApplicationOptions { AssemblyName = typeof(ElasticsearchResource).Assembly.FullName, DisableDashboard = true };
        var builder = DistributedApplication.CreateBuilder(options);

        // don't use random ports for tests
        builder.Configuration["DcpPublisher:RandomizePorts"] = "false";

        builder.AddElasticsearch("Elasticsearch", port: 9200)
            .WithContainerName("Exceptionless-Elasticsearch-Test")
            .WithImageTag("8.17.0")
            .WithLifetime(ContainerLifetime.Persistent);

        _app = builder.Build();

        return _app.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSolutionRelativeContentRoot("src/Exceptionless.Web");
    }

    protected override IHostBuilder CreateHostBuilder()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddYamlFile("appsettings.yml", optional: false, reloadOnChange: false)
            .Build();

        return Program.CreateHostBuilder(config, Environments.Development);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
