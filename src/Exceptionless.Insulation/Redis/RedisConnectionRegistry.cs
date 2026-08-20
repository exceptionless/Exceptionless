using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Exceptionless.Insulation.Redis;

internal sealed class RedisConnectionRegistry : IDisposable
{
    private readonly Dictionary<string, Lazy<IConnectionMultiplexer>> _connections = new(StringComparer.Ordinal);
    private readonly HashSet<IConnectionMultiplexer> _externallyOwnedConnections = new(ReferenceEqualityComparer.Instance);
    private readonly Func<string, ILoggerFactory, IConnectionMultiplexer> _connectionFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Lock _lock = new();
    private bool _disposed;

    public RedisConnectionRegistry(ILoggerFactory loggerFactory)
        : this(loggerFactory, static (connectionString, factory) =>
            ConnectionMultiplexer.Connect(connectionString, options => options.LoggerFactory = factory))
    {
    }

    internal RedisConnectionRegistry(
        ILoggerFactory loggerFactory,
        Func<string, ILoggerFactory, IConnectionMultiplexer> connectionFactory)
    {
        _loggerFactory = loggerFactory;
        _connectionFactory = connectionFactory;
    }

    public IConnectionMultiplexer GetConnection(string connectionString)
        => GetConnection(connectionString, externallyOwned: false);

    public IConnectionMultiplexer GetCacheConnection(string connectionString)
        => GetConnection(connectionString, externallyOwned: true);

    private IConnectionMultiplexer GetConnection(string connectionString, bool externallyOwned)
    {
        if (String.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("A Redis connection string is required.", nameof(connectionString));

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_connections.TryGetValue(connectionString, out Lazy<IConnectionMultiplexer>? connection))
            {
                connection = new Lazy<IConnectionMultiplexer>(
                    () => _connectionFactory(connectionString, _loggerFactory),
                    LazyThreadSafetyMode.ExecutionAndPublication);
                _connections.Add(connectionString, connection);
            }

            try
            {
                IConnectionMultiplexer multiplexer = connection.Value;
                if (externallyOwned)
                    _externallyOwnedConnections.Add(multiplexer);

                return multiplexer;
            }
            catch
            {
                _connections.Remove(connectionString);
                throw;
            }
        }
    }

    public void Dispose()
    {
        List<IConnectionMultiplexer> connectionsToDispose;
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            connectionsToDispose = _connections.Values
                .Where(connection => connection.IsValueCreated)
                .Select(connection => connection.Value)
                .Distinct((IEqualityComparer<IConnectionMultiplexer>)ReferenceEqualityComparer.Instance)
                .Where(connection => !_externallyOwnedConnections.Contains(connection))
                .ToList();
        }

        foreach (IConnectionMultiplexer multiplexer in connectionsToDispose)
            multiplexer.Dispose();
    }
}
