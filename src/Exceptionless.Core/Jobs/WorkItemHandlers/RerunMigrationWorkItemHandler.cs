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
            Log.LogWarning(ex, "Migration rerun operation {MigrationRerunOperationId} failed and will not be retried automatically", workItem.OperationId);
        }
    }
}
