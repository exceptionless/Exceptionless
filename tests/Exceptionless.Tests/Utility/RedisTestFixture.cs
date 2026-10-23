using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Xunit;

namespace Exceptionless.Tests.Utility;

public sealed class RedisTestFixture : IAsyncLifetime
{
    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;

    public string ConnectionString { get; private set; } = String.Empty;

    internal static Task<IDistributedApplicationTestingBuilder> CreateBuilderAsync(CancellationToken cancellationToken)
    {
        return DistributedApplicationTestingBuilder.CreateAsync<Projects.Exceptionless_AppHost>(
            ["--test-redis", "--Logging:LogLevel:Default=Warning"], cancellationToken);
    }

    public async ValueTask InitializeAsync()
    {
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        startup.CancelAfter(TimeSpan.FromMinutes(3));
        var cancellationToken = startup.Token;
        try
        {
            _builder = await CreateBuilderAsync(cancellationToken);
            _app = await _builder.BuildAsync(cancellationToken);
            await _app.StartAsync(cancellationToken);
            await _app.ResourceNotifications.WaitForResourceHealthyAsync("Redis", cancellationToken);
            ConnectionString = await _app.GetConnectionStringAsync("Redis", cancellationToken)
                ?? throw new InvalidOperationException("Redis did not expose a connection string.");
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        var app = _app;
        var builder = _builder;
        _app = null;
        _builder = null;
        try
        {
            if (app is not null)
                await app.DisposeAsync();
        }
        finally
        {
            if (builder is not null)
                await builder.DisposeAsync();
        }
    }
}
