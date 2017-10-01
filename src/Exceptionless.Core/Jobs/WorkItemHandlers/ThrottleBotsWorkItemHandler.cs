using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class ThrottleBotsWorkItemHandler : WorkItemHandlerBase {
        private readonly IEventRepository _eventRepository;
        private readonly ILockProvider _lockProvider;

        public ThrottleBotsWorkItemHandler(IEventRepository eventRepository, ICacheClient cacheClient, IMessageBus messageBus, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _eventRepository = eventRepository;
            _lockProvider = new CacheLockProvider(cacheClient, messageBus);
        }

        public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new CancellationToken()) {
            var wi = (ThrottleBotsWorkItem)workItem;
            string cacheKey = $"{nameof(ThrottleBotsWorkItemHandler)}:{wi.OrganizationId}:{wi.ClientIpAddress}";
            return _lockProvider.AcquireAsync(cacheKey, TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        public override Task HandleItemAsync(WorkItemContext context) {
            var workItem = context.GetData<ThrottleBotsWorkItem>();
            return _eventRepository.HideAllByClientIpAndDateAsync(workItem.OrganizationId, workItem.ClientIpAddress, workItem.UtcStartDate, workItem.UtcEndDate);
        }
    }
}