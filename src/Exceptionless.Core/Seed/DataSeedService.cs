using Foundatio.Extensions.Hosting.Startup;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Seed;

public class DataSeedService : IStartupAction
{
    private readonly IEnumerable<IDataSeed> _seeds;
    private readonly ILogger _logger;

    public DataSeedService(IEnumerable<IDataSeed> seeds, ILoggerFactory loggerFactory)
    {
        _seeds = seeds;
        _logger = loggerFactory.CreateLogger<DataSeedService>();
    }

    public Task RunAsync(CancellationToken shutdownToken = default)
    {
        return SeedAsync(shutdownToken);
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var seed in _seeds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Running data seed {DataSeedName}", seed.Name);
            await seed.SeedAsync(cancellationToken);
        }
    }
}
