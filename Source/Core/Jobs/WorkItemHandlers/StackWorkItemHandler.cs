using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class StackWorkItemHandler : WorkItemHandlerBase {
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ILockProvider _lockProvider;

        public StackWorkItemHandler(IStackRepository stackRepository, IEventRepository eventRepository, ICacheClient cacheClient) {
            _stackRepository = stackRepository;
            _eventRepository = eventRepository;
            _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromMinutes(15));
        }

        public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = new CancellationToken()) {
            return _lockProvider.AcquireAsync(nameof(StackWorkItemHandler), TimeSpan.FromMinutes(15), new CancellationToken(true));
        }

        public override async Task HandleItemAsync(WorkItemContext context) {
            var workItem = context.GetData<StackWorkItem>();
            if (workItem.Delete) {
                await _eventRepository.RemoveAllByStackIdsAsync(new[] { workItem.StackId });
                await _stackRepository.RemoveAsync(workItem.StackId);
                return;
            }

            if (workItem.UpdateIsFixed)
                await _eventRepository.UpdateFixedByStackAsync(workItem.OrganizationId, workItem.StackId, workItem.IsFixed).AnyContext();

            if (workItem.UpdateIsHidden)
                await _eventRepository.UpdateHiddenByStackAsync(workItem.OrganizationId, workItem.StackId, workItem.IsHidden).AnyContext();
        }
    }
}