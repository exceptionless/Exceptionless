using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Jobs;

namespace Exceptionless.Core.Jobs.WorkItemHandlers {
    public class SetLocationFromGeoWorkItemHandler : WorkItemHandlerBase {
        private readonly IEventRepository _eventRepository;

        public SetLocationFromGeoWorkItemHandler(IEventRepository eventRepository) {
            _eventRepository = eventRepository;
        }

        public override Task HandleItemAsync(WorkItemContext context) {
            var workItem = context.GetData<SetLocationFromGeoWorkItem>();
            
            // TODO Look up geo via third party service.

            // Update the event.

            return Task.CompletedTask;
        }
    }
}