namespace Exceptionless.Core.Seed;

public interface IDataSeed
{
    string Name { get; }

    Task SeedAsync(CancellationToken cancellationToken = default);
}
