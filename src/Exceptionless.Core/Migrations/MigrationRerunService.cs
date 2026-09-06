using System.Diagnostics;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

public sealed class MigrationRerunService(
    MigrationManager migrationManager,
    IMigrationStateRepository migrationStateRepository,
    ICacheClient cache,
    ILockProvider lockProvider,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
{
    private const string MigrationLockName = "migration-manager";
    private static readonly TimeSpan OperationRetention = TimeSpan.FromDays(7);
    private static readonly TimeSpan MigrationLockDuration = TimeSpan.FromMinutes(30);
    private readonly ILogger _logger = loggerFactory.CreateLogger<MigrationRerunService>();

    public IReadOnlySet<string> GetRerunnableMigrationIds()
    {
        EnsureMigrationsLoaded();
        return migrationManager.Migrations
            .Where(migration => migration is IRerunnableMigration)
            .Select(migration => migration.GetId())
            .Where(id => !String.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<MigrationRerunOperation> QueueAsync(
        string migrationId,
        MigrationRerunSource source,
        string? requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        var migration = GetRerunnableMigration(migrationId);
        var state = await migrationStateRepository.GetByIdAsync(migrationId);
        if (state?.CompletedUtc is null)
            throw new InvalidOperationException($"Migration '{migrationId}' must be completed before it can be rerun.");

        string operationId = Guid.NewGuid().ToString("N");
        if (!await cache.AddAsync(GetActiveOperationCacheKey(migrationId), operationId, OperationRetention))
            throw new MigrationRerunAlreadyActiveException(migrationId);

        var operation = new MigrationRerunOperation
        {
            Id = operationId,
            MigrationId = migrationId,
            Version = migration.Version!.Value,
            Source = source,
            Status = MigrationRerunStatus.Queued,
            RequestedByUserId = requestedByUserId,
            RequestedUtc = timeProvider.GetUtcNow().UtcDateTime
        };

        try
        {
            await SaveOperationAsync(operation);
            _logger.LogInformation(
                "Queued migration rerun {MigrationRerunOperationId} for migration {MigrationId}. Source: {MigrationRerunSource} RequestedByUserId: {RequestedByUserId}",
                operation.Id,
                operation.MigrationId,
                operation.Source,
                operation.RequestedByUserId);
            return operation;
        }
        catch
        {
            await cache.RemoveAsync(GetActiveOperationCacheKey(migrationId));
            throw;
        }
    }

    public Task<MigrationRerunOperation?> GetOperationAsync(string operationId)
        => cache.GetAsync<MigrationRerunOperation?>(GetOperationCacheKey(operationId), null);

    public async Task FailQueuedOperationAsync(string operationId, Exception exception)
    {
        var operation = await GetOperationAsync(operationId);
        if (operation is null || operation.Status != MigrationRerunStatus.Queued)
            return;

        operation.Status = MigrationRerunStatus.Failed;
        operation.CompletedUtc = timeProvider.GetUtcNow().UtcDateTime;
        operation.ErrorMessage = exception.Message.Length > 1000 ? exception.Message[..1000] : exception.Message;
        await SaveOperationAsync(operation);
        await cache.RemoveAsync(GetActiveOperationCacheKey(operation.MigrationId));
        _logger.LogError(
            exception,
            "Failed to dispatch migration rerun {MigrationRerunOperationId} for migration {MigrationId}",
            operation.Id,
            operation.MigrationId);
    }

    public async Task<MigrationRerunOperation> RunAsync(string operationId, CancellationToken cancellationToken = default)
    {
        var operation = await GetOperationAsync(operationId)
            ?? throw new KeyNotFoundException($"Migration rerun operation '{operationId}' was not found.");
        if (operation.Status is MigrationRerunStatus.Completed or MigrationRerunStatus.Failed or MigrationRerunStatus.Cancelled)
        {
            return operation;
        }

        var migration = GetRerunnableMigration(operation.MigrationId);

        await using var migrationLock = await lockProvider.TryAcquireAsync(
            MigrationLockName,
            MigrationLockDuration,
            cancellationToken);
        if (migrationLock is null)
        {
            if (operation.Status == MigrationRerunStatus.Running)
            {
                throw new MigrationRerunRecoveryPendingException(operation.Id);
            }

            var exception = new InvalidOperationException("Another migration is currently running. Try the rerun again after it completes.");
            await FailQueuedOperationAsync(operation.Id, exception);
            throw exception;
        }

        operation.Status = MigrationRerunStatus.Running;
        operation.StartedUtc = timeProvider.GetUtcNow().UtcDateTime;
        operation.AttemptCount++;
        operation.ErrorMessage = null;
        await SaveOperationAsync(operation);

        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Starting migration rerun {MigrationRerunOperationId} for migration {MigrationId}. Attempt: {MigrationRerunAttempt} Source: {MigrationRerunSource} RequestedByUserId: {RequestedByUserId}",
            operation.Id,
            operation.MigrationId,
            operation.AttemptCount,
            operation.Source,
            operation.RequestedByUserId);

        try
        {
            await migration.RunAsync(new MigrationContext(
                migrationLock,
                loggerFactory.CreateLogger(migration.GetType()),
                cancellationToken));

            operation.Status = MigrationRerunStatus.Completed;
            operation.CompletedUtc = timeProvider.GetUtcNow().UtcDateTime;
            await SaveOperationAsync(operation);
            _logger.LogInformation(
                "Completed migration rerun {MigrationRerunOperationId} for migration {MigrationId} in {MigrationRerunDuration}",
                operation.Id,
                operation.MigrationId,
                stopwatch.Elapsed);
            return operation;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            operation.Status = MigrationRerunStatus.Cancelled;
            operation.CompletedUtc = timeProvider.GetUtcNow().UtcDateTime;
            await SaveOperationAsync(operation);
            throw;
        }
        catch (Exception ex)
        {
            operation.Status = MigrationRerunStatus.Failed;
            operation.CompletedUtc = timeProvider.GetUtcNow().UtcDateTime;
            operation.ErrorMessage = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            await SaveOperationAsync(operation);
            _logger.LogError(
                ex,
                "Failed migration rerun {MigrationRerunOperationId} for migration {MigrationId} after {MigrationRerunDuration}",
                operation.Id,
                operation.MigrationId,
                stopwatch.Elapsed);
            throw;
        }
        finally
        {
            await cache.RemoveAsync(GetActiveOperationCacheKey(operation.MigrationId));
        }
    }

    private IMigration GetRerunnableMigration(string migrationId)
    {
        EnsureMigrationsLoaded();
        var migration = migrationManager.Migrations.FirstOrDefault(candidate =>
            String.Equals(candidate.GetId(), migrationId, StringComparison.OrdinalIgnoreCase));
        if (migration is null)
            throw new KeyNotFoundException($"Migration '{migrationId}' was not found.");
        if (migration is not IRerunnableMigration)
            throw new InvalidOperationException($"Migration '{migrationId}' does not support reruns.");

        return migration;
    }

    private void EnsureMigrationsLoaded()
    {
        if (migrationManager.Migrations.Count == 0)
            migrationManager.AddMigrationsFromLoadedAssemblies();
    }

    private Task SaveOperationAsync(MigrationRerunOperation operation)
    {
        return cache.SetAsync(GetOperationCacheKey(operation.Id), operation, OperationRetention);
    }

    private static string GetActiveOperationCacheKey(string migrationId) => $"migration-rerun:active:{migrationId}";
    private static string GetOperationCacheKey(string operationId) => $"migration-rerun:operation:{operationId}";
}

public sealed class MigrationRerunAlreadyActiveException(string migrationId)
    : Exception($"Migration '{migrationId}' already has a queued or running rerun.");

public sealed class MigrationRerunRecoveryPendingException(string operationId)
    : Exception($"Migration rerun operation '{operationId}' is still running and will be retried.");
