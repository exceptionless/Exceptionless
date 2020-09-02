using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class RemoveStacksWorkItemHandler : WorkItemHandlerBase {
        private readonly IStackRepository _stackRepository;
        private readonly ILockProvider _lockProvider;

        public RemoveStacksWorkItemHandler(IStackRepository stackRepository, ICacheClient cacheClient, IMessageBus messageBus, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _stackRepository = stackRepository;
            _lockProvider = new CacheLockProvider(cacheClient, messageBus);
        }

        public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new CancellationToken()) {
            string cacheKey = $"{nameof(RemoveStacksWorkItem)}:{((RemoveStacksWorkItem)workItem).ProjectId}";
            return _lockProvider.AcquireAsync(cacheKey, TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        public override async Task HandleItemAsync(WorkItemContext context) {
            var wi = context.GetData<RemoveStacksWorkItem>();
            using (Log.BeginScope(new ExceptionlessState().Organization(wi.OrganizationId).Project(wi.ProjectId))) {
                Log.LogInformation("Received remove stacks work item for project: {ProjectId}", wi.ProjectId);
                await context.ReportProgressAsync(0, "Starting soft deleting of stacks...").AnyContext();
                long deleted = await _stackRepository.SoftDeleteByProjectIdAsync(wi.OrganizationId, wi.ProjectId).AnyContext();
                await context.ReportProgressAsync(100, $"Stacks soft deleted: {deleted}").AnyContext();
            }
        }
    }
}