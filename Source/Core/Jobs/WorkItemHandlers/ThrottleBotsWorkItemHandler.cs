using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class ThrottleBotsWorkItemHandler : WorkItemHandlerBase {
        private readonly IEventRepository _eventRepository;
        private readonly ILockProvider _lockProvider;

        public ThrottleBotsWorkItemHandler(IEventRepository eventRepository, ICacheClient cacheClient) {
            _eventRepository = eventRepository;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromMinutes(15));
        }

        public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new CancellationToken()) {
            return _lockProvider.AcquireAsync(nameof(ThrottleBotsWorkItemHandler), TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        public override Task HandleItemAsync(WorkItemContext context) {
            var workItem = context.GetData<ThrottleBotsWorkItem>();
            return _eventRepository.HideAllByClientIpAndDateAsync(workItem.OrganizationId, workItem.ClientIpAddress, workItem.UtcStartDate, workItem.UtcEndDate);
        }
    }
}