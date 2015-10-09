using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Jobs;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class ThrottleBotsWorkItemHandler : WorkItemHandlerBase {
        private readonly IEventRepository _eventRepository;

        public ThrottleBotsWorkItemHandler(IEventRepository eventRepository) {
            _eventRepository = eventRepository;
        }

        public override Task HandleItemAsync(WorkItemContext context) {
            var workItem = context.GetData<ThrottleBotsWorkItem>();
            return _eventRepository.HideAllByClientIpAndDateAsync(workItem.OrganizationId, workItem.ClientIpAddress, workItem.UtcStartDate, workItem.UtcEndDate);
        }
    }
}