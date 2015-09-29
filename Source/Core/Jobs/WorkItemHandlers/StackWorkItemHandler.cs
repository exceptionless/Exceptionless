using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Jobs;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class StackWorkItemHandler : WorkItemHandlerBase {
        private readonly IStackRepository _stackRepository;
        private readonly IEventRepository _eventRepository;

        public StackWorkItemHandler(IStackRepository stackRepository, IEventRepository eventRepository) {
            _stackRepository = stackRepository;
            _eventRepository = eventRepository;
        }

        public override async Task HandleItemAsync(WorkItemContext context, CancellationToken cancellationToken = new CancellationToken()) {
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