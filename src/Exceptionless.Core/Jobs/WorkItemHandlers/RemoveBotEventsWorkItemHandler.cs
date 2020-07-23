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

namespace Exceptionless.Core.Jobs.WorkItemHandlers
{
    public class RemoveBotEventsWorkItemHandler : WorkItemHandlerBase {
        private readonly IEventRepository _eventRepository;
        private readonly ILockProvider _lockProvider;

        public RemoveBotEventsWorkItemHandler(IEventRepository eventRepository, ICacheClient cacheClient, IMessageBus messageBus, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _eventRepository = eventRepository;
            _lockProvider = new CacheLockProvider(cacheClient, messageBus);
        }

        public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new CancellationToken()) {
            string cacheKey = $"{nameof(RemoveBotEventsWorkItem)}:{((RemoveBotEventsWorkItem)workItem).OrganizationId}";
            return _lockProvider.AcquireAsync(cacheKey, TimeSpan.FromMinutes(15), cancellationToken);
        }

        public override async Task HandleItemAsync(WorkItemContext context) {
            var wi = context.GetData<RemoveBotEventsWorkItem>();
            using (Log.BeginScope(new ExceptionlessState().Organization(wi.OrganizationId))) {
                Log.LogInformation("Received remove bot events work item OrganizationId={OrganizationId}, ClientIpAddress={ClientIpAddress}, UtcStartDate={UtcStartDate}, UtcEndDate={UtcEndDate}", wi.OrganizationId, wi.ClientIpAddress, wi.UtcStartDate, wi.UtcEndDate);
                await context.ReportProgressAsync(0, "Starting deleting of bot events...").AnyContext();
                long deleted = await _eventRepository.RemoveAllAsync(new [] { wi.OrganizationId }, null, null, null, wi.ClientIpAddress, wi.UtcStartDate, wi.UtcEndDate).AnyContext();
                await context.ReportProgressAsync(100, $"Bot events deleted: {deleted}").AnyContext();
            }
        }
    }
}