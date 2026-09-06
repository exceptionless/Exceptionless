using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Models.WorkItems;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories.Migrations;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public sealed class MigrationRerunServiceTests : IntegrationTestsBase
{
    private const int CancellableMigrationVersion = 1000;

    public MigrationRerunServiceTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    protected override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<CancellableRerunnableMigration>();
        base.RegisterServices(services);
    }

    [Fact]
    public Task HandleItemAsync_MissingOperation_ThrowsForQueueRetry()
    {
        // Arrange
        var workItem = new RerunMigrationWorkItem { OperationId = "missing-operation" };
        var context = new WorkItemContext(
            workItem,
            "test-job",
            EmptyLock.Empty,
            TestCancellationToken,
            static (_, _) => Task.CompletedTask);

        // Act / Assert
        return Assert.ThrowsAsync<KeyNotFoundException>(() =>
            GetService<RerunMigrationWorkItemHandler>().HandleItemAsync(context));
    }

    [Fact]
    public async Task RunAsync_CancelledUserInterfaceOperation_RemainsRetryable()
    {
        // Arrange
        var migration = GetService<CancellableRerunnableMigration>();
        migration.Reset();
        GetService<MigrationManager>().Migrations.Add(migration);

        var completedUtc = DateTime.UtcNow.AddDays(-1);
        await GetService<IMigrationStateRepository>().AddAsync(new MigrationState
        {
            Id = CancellableMigrationVersion.ToString(),
            Version = CancellableMigrationVersion,
            MigrationType = MigrationType.VersionedAndResumable,
            StartedUtc = completedUtc.AddMinutes(-1),
            CompletedUtc = completedUtc
        });

        var rerunService = GetService<MigrationRerunService>();
        var operation = await rerunService.QueueAsync(
            CancellableMigrationVersion.ToString(),
            MigrationRerunSource.UserInterface,
            null,
            TestCancellationToken);
        using var cancellationTokenSource = new CancellationTokenSource();

        // Act
        var interruptedRun = rerunService.RunAsync(operation.Id, cancellationTokenSource.Token);
        await migration.Started.WaitAsync(TestCancellationToken);
        await cancellationTokenSource.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => interruptedRun);

        operation = await rerunService.GetOperationAsync(operation.Id);
        Assert.NotNull(operation);
        Assert.Equal(MigrationRerunStatus.Running, operation.Status);
        Assert.Null(operation.CompletedUtc);
        Assert.Equal(1, operation.AttemptCount);
        await Assert.ThrowsAsync<MigrationRerunAlreadyActiveException>(() => rerunService.QueueAsync(
            CancellableMigrationVersion.ToString(),
            MigrationRerunSource.UserInterface,
            null,
            TestCancellationToken));

        migration.CompleteImmediately = true;
        operation = await rerunService.RunAsync(operation.Id, TestCancellationToken);

        // Assert
        Assert.Equal(MigrationRerunStatus.Completed, operation.Status);
        Assert.Equal(2, operation.AttemptCount);
    }
}

internal sealed class CancellableRerunnableMigration : MigrationBase, IRerunnableMigration
{
    private TaskCompletionSource _started = CreateCompletionSource();

    public CancellableRerunnableMigration(ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        MigrationType = MigrationType.VersionedAndResumable;
        Version = 1000;
    }

    public Task Started => _started.Task;
    public bool CompleteImmediately { get; set; }

    public override async Task RunAsync(MigrationContext context)
    {
        if (CompleteImmediately)
        {
            return;
        }

        _started.TrySetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken);
    }

    public void Reset()
    {
        CompleteImmediately = false;
        _started = CreateCompletionSource();
    }

    private static TaskCompletionSource CreateCompletionSource()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
