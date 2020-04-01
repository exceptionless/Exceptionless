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
    [Job(Description = "Processes queued event deletions.", InitialDelay = "10s")]
    public class EventDeletionJob : QueueJobBase<EventDeletion> {
        private readonly IEventRepository _eventRepository;

        public EventDeletionJob(IQueue<EventDeletion> queue, IEventRepository eventRepository, ILoggerFactory loggerFactory = null) : base(queue, loggerFactory) {
            _eventRepository = eventRepository;
        }

        protected override async Task<JobResult> ProcessQueueEntryAsync(QueueEntryContext<EventDeletion> context) {
            _logger.LogTrace("Processing event deletion: id={0}", context.QueueEntry.Id);

            var ed = context.QueueEntry.Value;
            try {
                long removed = await _eventRepository.RemoveAllAsync(ed.OrganizationIds, ed.ProjectIds, ed.StackIds, ed.EventIds, ed.ClientIpAddress, ed.UtcStartDate, ed.UtcEndDate).AnyContext();
                _logger.LogInformation("Processed event deletion: id={Id}, removed={Removed}", context.QueueEntry.Id, removed);
            } catch (Exception ex) {
                _logger.LogError(ex, "Error processing event deletion {Id}: {Message}", context.QueueEntry.Id, ex.Message);
                return JobResult.FromException(ex);
            }

            return JobResult.Success;
        }
    }
}