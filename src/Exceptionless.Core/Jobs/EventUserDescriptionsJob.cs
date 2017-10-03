using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Base;
using Exceptionless.Core.Models.Data;
using Foundatio.Jobs;
using Foundatio.Queues;
using Microsoft.Extensions.Logging;

#pragma warning disable 1998

namespace Exceptionless.Core.Jobs {
    [Job(Description = "Processes queued event user descriptions.", InitialDelay = "3s")]
    public class EventUserDescriptionsJob : QueueJobBase<EventUserDescription> {
        private readonly IEventRepository _eventRepository;

        public EventUserDescriptionsJob(IQueue<EventUserDescription> queue, IEventRepository eventRepository, ILoggerFactory loggerFactory = null) : base(queue, loggerFactory) {
            _eventRepository = eventRepository;
        }

        protected override async Task<JobResult> ProcessQueueEntryAsync(QueueEntryContext<EventUserDescription> context) {
            _logger.LogTrace("Processing user description: id={0}", context.QueueEntry.Id);

            try {
                await ProcessUserDescriptionAsync(context.QueueEntry.Value).AnyContext();
                _logger.LogInformation("Processed user description: id={Id}", context.QueueEntry.Id);
            } catch (DocumentNotFoundException ex){
                _logger.LogError(ex, "An event with this reference id {ReferenceId} has not been processed yet or was deleted. Queue Id: {Id}", ex.Id, context.QueueEntry.Id);
                return JobResult.FromException(ex);
            } catch (Exception ex) {
                _logger.LogError(ex, "An error occurred while processing the EventUserDescription {Id}: {Message}", context.QueueEntry.Id, ex.Message);
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