using Exceptionless.Core.Migrations;
using Exceptionless.Core.Models.WorkItems;
using Foundatio.Jobs;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

public sealed class RerunMigrationWorkItemHandler(
    MigrationRerunService migrationRerunService,
    ILoggerFactory loggerFactory) : WorkItemHandlerBase(loggerFactory)
{
    public override async Task HandleItemAsync(WorkItemContext context)
    {
        var workItem = context.GetData<RerunMigrationWorkItem>()!;
        try
        {
            await migrationRerunService.RunAsync(workItem.OperationId, context.CancellationToken);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (MigrationRerunRecoveryPendingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var operation = await migrationRerunService.GetOperationAsync(workItem.OperationId);
            if (operation?.Status != MigrationRerunStatus.Failed)
            {
                Log.LogWarning(ex, "Migration rerun operation {MigrationRerunOperationId} was interrupted before a failure was recorded and will be retried", workItem.OperationId);
                throw;
            }

            Log.LogWarning(ex, "Migration rerun operation {MigrationRerunOperationId} failed and will not be retried automatically", workItem.OperationId);
        }
    }
}
