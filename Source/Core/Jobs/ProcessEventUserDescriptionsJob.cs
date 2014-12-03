using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeSmith.Core.Extensions;
using CodeSmith.Core.Scheduler;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Base;
using Exceptionless.Models.Data;
using NLog.Fluent;

namespace Exceptionless.Core.Jobs {
    public class ProcessEventUserDescriptionsJob : JobBase {
        private readonly IQueue<EventUserDescription> _queue;
        private readonly IEventRepository _eventRepository;
        private readonly IAppStatsClient _statsClient;

        public ProcessEventUserDescriptionsJob(IQueue<EventUserDescription> queue, IEventRepository eventRepository, IAppStatsClient statsClient) {
            _queue = queue;
            _eventRepository = eventRepository;
            _statsClient = statsClient;
        }

        protected async override Task<JobResult> RunInternalAsync(CancellationToken token) {
            Log.Info().Message("Process user description job starting").Write();

            QueueEntry<EventUserDescription> queueEntry = null;
            try {
                queueEntry = _queue.Dequeue();
            } catch (Exception ex) {
                if (!(ex is TimeoutException)) {
                    Log.Error().Exception(ex).Message("An error occurred while trying to dequeue the next EventUserDescription: {0}", ex.Message).Write();
                    return JobResult.FromException(ex);
                }
            }
            if (queueEntry == null)
                return JobResult.Success;
                
            _statsClient.Counter(StatNames.EventsUserDescriptionDequeued);
            Log.Info().Message("Processing EventUserDescription '{0}'.", queueEntry.Id).Write();

            try {
                ProcessUserDescription(queueEntry.Value);
                _statsClient.Counter(StatNames.EventsUserDescriptionProcessed);
            } catch (DocumentNotFoundException ex){
                _statsClient.Counter(StatNames.EventsUserDescriptionErrors);
                queueEntry.Abandon();
                Log.Error().Exception(ex).Message("An event with this reference id \"{0}\" has not been processed yet or was deleted. Queue Id: {1}", ex.Id, queueEntry.Id).Write();
                return JobResult.FromException(ex);
            } catch (Exception ex) {
                _statsClient.Counter(StatNames.EventsUserDescriptionErrors);
                queueEntry.Abandon();

                // TODO: Add the EventUserDescription to the logged exception.
                Log.Error().Exception(ex).Message("An error occurred while processing the EventUserDescription '{0}': {1}", queueEntry.Id, ex.Message).Write();
                return JobResult.FromException(ex);
            }

            queueEntry.Complete();

            return JobResult.Success;
        }

        private void ProcessUserDescription(EventUserDescription description) {
            var ev = _eventRepository.GetByReferenceId(description.ProjectId, description.ReferenceId).FirstOrDefault();
            if (ev == null)
                throw new DocumentNotFoundException(description.ReferenceId);

            var ud = new UserDescription {
                EmailAddress = description.EmailAddress,
                Description = description.Description
            };

            if (description.Data.Count > 0)
                ev.Data.AddRange(description.Data);

            ev.SetUserDescription(ud);

            _eventRepository.Save(ev);
        }
    }
}