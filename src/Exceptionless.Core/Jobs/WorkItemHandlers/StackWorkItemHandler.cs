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
    public class StackWorkItemHandler : WorkItemHandlerBase {
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ILockProvider _lockProvider;

        public StackWorkItemHandler(IStackRepository stackRepository, IEventRepository eventRepository, ICacheClient cacheClient, IMessageBus messageBus, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _stackRepository = stackRepository;
            _eventRepository = eventRepository;
            _lockProvider = new CacheLockProvider(cacheClient, messageBus);
        }

        public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new CancellationToken()) {
            string cacheKey = $"{nameof(StackWorkItemHandler)}:{((StackWorkItem)workItem).StackId}";
            return _lockProvider.AcquireAsync(cacheKey, TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        public override async Task HandleItemAsync(WorkItemContext context) {
            var wi = context.GetData<StackWorkItem>();
            using (Log.BeginScope(new ExceptionlessState().Organization(wi.OrganizationId).Project(wi.ProjectId))) {
                if (wi.Delete) {
                    await _eventRepository.RemoveAllByStackIdAsync(wi.OrganizationId, wi.ProjectId, wi.StackId).AnyContext();
                    await _stackRepository.RemoveAsync(wi.StackId).AnyContext();
                    return;
                }

                if (wi.UpdateIsFixed)
                    await _eventRepository.UpdateFixedByStackAsync(wi.OrganizationId, wi.ProjectId, wi.StackId, wi.IsFixed).AnyContext();

                if (wi.UpdateIsHidden)
                    await _eventRepository.UpdateHiddenByStackAsync(wi.OrganizationId, wi.ProjectId, wi.StackId, wi.IsHidden).AnyContext();
            }
        }
    }
}