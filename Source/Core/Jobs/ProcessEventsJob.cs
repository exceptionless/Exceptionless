using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CodeSmith.Core.Scheduler;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Queues;
using Exceptionless.Models;
using NLog.Fluent;

namespace Exceptionless.Core.Jobs {
    public class ProcessEventsJob : Job {
        private readonly IQueue<EventPost> _queue;
        private readonly EventParserPluginManager _eventParserPluginManager;
        private readonly EventPipeline _eventPipeline;

        public ProcessEventsJob(IQueue<EventPost> queue, EventParserPluginManager eventParserPluginManager, EventPipeline eventPipeline) {
            _queue = queue;
            _eventParserPluginManager = eventParserPluginManager;
            _eventPipeline = eventPipeline;
        }

        public async override Task<JobResult> RunAsync(JobRunContext context) {
            Log.Info().Message("Process events job starting").Write();

            WorkItem<EventPost> workItem = null;
            try {
                workItem = await _queue.DequeueAsync();
            } catch (Exception ex) {
                if (!(ex is TimeoutException)) {
                    Log.Error().Exception(ex).Message("An error occurred while trying to dequeue the next EventPost: {0}", ex.Message).Write();
                    return JobResult.FromException(ex);
                }
            }
            if (workItem == null)
                return JobResult.Success;

            Log.Info().Message("Processing EventPost '{0}'.", workItem.Id).Write();

            List<PersistentEvent> events;
            try {
                events = ProcessEventPost(workItem.Value);
            } catch (Exception ex) {
                workItem.AbandonAsync().Wait();

                // TODO: Add the EventPost to the logged exception.
                Log.Error().Exception(ex).Message("An error occurred while processing the EventPost '{0}': {1}", workItem.Id, ex.Message).Write();
                return JobResult.FromException(ex);
            }

            bool isSingleEvent = events.Count == 1;
            int errorCount = 0;
            foreach (PersistentEvent ev in events) {
                try {
                    _eventPipeline.Run(ev);
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("An error occurred while processing the EventPipeline: {0}", ex.Message).Write();

                    if (!isSingleEvent) {
                        // Put this single event back into the queue so we can retry it separately.
                        _queue.EnqueueAsync(new EventPost {
                            Data = Encoding.UTF8.GetBytes(ev.ToJson()).Compress(),
                            ProjectId = ev.ProjectId,
                            CharSet = "utf-8",
                            MediaType = "application/json",
                        }).Wait();
                    }

                    errorCount++;
                }
            }

            if (isSingleEvent && errorCount > 0)
                workItem.AbandonAsync().Wait();
            else
                workItem.CompleteAsync().Wait();

            return JobResult.Success;
        }

        private List<PersistentEvent> ProcessEventPost(EventPost ep) {
            byte[] data = ep.Data.Decompress();

            var encoding = Encoding.UTF8;
            if (!String.IsNullOrEmpty(ep.CharSet))
                encoding = Encoding.GetEncoding(ep.CharSet);

            string input = encoding.GetString(data);
            List<PersistentEvent> events = _eventParserPluginManager.ParseEvents(input);
            events.ForEach(e => {
                // set the project id on all events
                e.ProjectId = ep.ProjectId;
            });

            return events;
        }
    }
}