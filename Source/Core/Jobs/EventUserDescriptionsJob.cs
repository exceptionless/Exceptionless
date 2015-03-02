using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Base;
using Exceptionless.Core.Models.Data;
using Foundatio.Jobs;
using Foundatio.Metrics;
using Foundatio.Queues;
using NLog.Fluent;
#pragma warning disable 1998

namespace Exceptionless.Core.Jobs {
    public class EventUserDescriptionsJob : JobBase {
        private readonly IQueue<EventUserDescription> _queue;
        private readonly IEventRepository _eventRepository;
        private readonly IMetricsClient _statsClient;

        public EventUserDescriptionsJob(IQueue<EventUserDescription> queue, IEventRepository eventRepository, IMetricsClient statsClient) {
            _queue = queue;
            _eventRepository = eventRepository;
            _statsClient = statsClient;
        }

        protected async override Task<JobResult> RunInternalAsync(CancellationToken token) {
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
                
            _statsClient.Counter(MetricNames.EventsUserDescriptionDequeued);
            Log.Trace().Message("Processing user description: id={0}", queueEntry.Id).Write();

            try {
                ProcessUserDescription(queueEntry.Value);
                Log.Info().Message("Processed user description: id={0}", queueEntry.Id).Write();
                _statsClient.Counter(MetricNames.EventsUserDescriptionProcessed);
            } catch (DocumentNotFoundException ex){
                _statsClient.Counter(MetricNames.EventsUserDescriptionErrors);
                queueEntry.Abandon();
                Log.Error().Exception(ex).Message("An event with this reference id \"{0}\" has not been processed yet or was deleted. Queue Id: {1}", ex.Id, queueEntry.Id).Write();
                return JobResult.FromException(ex);
            } catch (Exception ex) {
                _statsClient.Counter(MetricNames.EventsUserDescriptionErrors);
                queueEntry.Abandon();

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