using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Base;
using Exceptionless.Core.Models.Data;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Metrics;
using Foundatio.Queues;

#pragma warning disable 1998

namespace Exceptionless.Core.Jobs {
    public class EventUserDescriptionsJob : QueueProcessorJobBase<EventUserDescription> {
        private readonly IEventRepository _eventRepository;
        private readonly IMetricsClient _metricsClient;

        public EventUserDescriptionsJob(IQueue<EventUserDescription> queue, IEventRepository eventRepository, IMetricsClient metricsClient) : base(queue) {
            _eventRepository = eventRepository;
            _metricsClient = metricsClient;
        }

        protected override async Task<JobResult> ProcessQueueEntryAsync(JobQueueEntryContext<EventUserDescription> context) {
            Logger.Trace().Message("Processing user description: id={0}", context.QueueEntry.Id).Write();

            try {
                await ProcessUserDescriptionAsync(context.QueueEntry.Value).AnyContext();
                Logger.Info().Message("Processed user description: id={0}", context.QueueEntry.Id).Write();
            } catch (DocumentNotFoundException ex){
                Logger.Error().Exception(ex).Message("An event with this reference id \"{0}\" has not been processed yet or was deleted. Queue Id: {1}", ex.Id, context.QueueEntry.Id).Write();
                return JobResult.FromException(ex);
            } catch (Exception ex) {
                Logger.Error().Exception(ex).Message("An error occurred while processing the EventUserDescription '{0}': {1}", context.QueueEntry.Id, ex.Message).Write();
                return JobResult.FromException(ex);
            }

            return JobResult.Success;
        }
        
        private async Task ProcessUserDescriptionAsync(EventUserDescription description) {
            var ev = (await _eventRepository.GetByReferenceIdAsync(description.ProjectId, description.ReferenceId).AnyContext()).Documents.FirstOrDefault();
            if (ev == null)
                throw new DocumentNotFoundException(description.ReferenceId);

            var ud = new UserDescription {
                EmailAddress = description.EmailAddress,
                Description = description.Description
            };

            if (description.Data.Count > 0)
                ev.Data.AddRange(description.Data);

            ev.SetUserDescription(ud);

            await _eventRepository.SaveAsync(ev).AnyContext();
        }
    }
}