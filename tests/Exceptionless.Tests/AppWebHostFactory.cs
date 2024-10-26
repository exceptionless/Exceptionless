using Aspire.Hosting;
using Aspire.Hosting.Testing;
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

    public string? ElasticsearchConnectionString { get; private set; }
    public string? RedisConnectionString { get; private set; }
    public string? MailConnectionString { get; private set; }

    public async Task InitializeAsync()
    {
        var options = new DistributedApplicationOptions { AssemblyName = typeof(ElasticsearchResource).Assembly.FullName, DisableDashboard = true };
        var builder = DistributedApplication.CreateBuilder(options);

        var elastic = builder.AddElasticsearch("Elasticsearch")
            .WithContainerName("Exceptionless-Elasticsearch-Test");

        var cache = builder.AddRedis("Redis")
            .WithContainerName("Exceptionless-Redis-Test");

        _app = builder.Build();

        await _app.StartAsync();

        ElasticsearchConnectionString = await _app.GetConnectionStringAsync("Elasticsearch");
        RedisConnectionString = await _app.GetConnectionStringAsync("Redis");
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
