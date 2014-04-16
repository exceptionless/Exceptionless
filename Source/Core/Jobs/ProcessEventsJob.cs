using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeSmith.Core.Extensions;
using CodeSmith.Core.Scheduler;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Queues;
using Newtonsoft.Json;
using Exceptionless.Models;
using NLog.Fluent;
using Encoding = System.Text.Encoding;

namespace Exceptionless.Core.Jobs {
    public class ProcessEventsJob : Job {
        private readonly IQueue<EventPost> _queue;
        private readonly EventPipeline _eventPipeline;

        public ProcessEventsJob(IQueue<EventPost> queue, EventPipeline eventPipeline) {
            _queue = queue;
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

            List<Event> events;
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
            foreach (Event ev in events) {
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

        private List<Event> ProcessEventPost(EventPost ep) {
            byte[] data = ep.Data.Decompress();

            var encoding = Encoding.UTF8;
            if (!String.IsNullOrEmpty(ep.CharSet))
                encoding = Encoding.GetEncoding(ep.CharSet);

            string result = encoding.GetString(data);
            List<Event> events = GetEventsFromString(result);

            events.ForEach(e => {
                // set the project id on all events
                e.ProjectId = ep.ProjectId;
            });

            return events;
        }

        public static List<Event> GetEventsFromString(string input) {
            var events = new List<Event>();
            switch (input.GetJsonType()) {
                case JsonType.None: {
                    events.AddRange(GetLogEvents(input));
                    break;
                }
                case JsonType.Object: {
                    Event ev;
                    if (input.TryFromJson(out ev, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error }))
                        events.Add(ev);
                    else
                        events.AddRange(GetLogEvents(input));
                    break;
                }
                case JsonType.Array: {
                    Event[] parsedEvents;
                    if (input.TryFromJson(out parsedEvents, new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error }))
                        events.AddRange(parsedEvents);
                    else
                        events.AddRange(GetLogEvents(input));
                    break;
                }
            }

            // set defaults for empty values
            events.ForEach(e => {
                if (e.Date == DateTimeOffset.MinValue)
                    e.Date = DateTimeOffset.Now;
                if (String.IsNullOrWhiteSpace(e.Type))
                    e.Type = e.Data.ContainsKey(Event.KnownDataKeys.Error) || e.Data.ContainsKey(Event.KnownDataKeys.SimpleError) ? Event.KnownTypes.Error : Event.KnownTypes.Log;
            });

            return events;
        }

        private static IEnumerable<Event> GetLogEvents(string input) {
            var events = new List<Event>();
            foreach (var entry in input.SplitAndTrim(new[] { Environment.NewLine }).Where(line => !String.IsNullOrWhiteSpace(line))) {
                events.Add(new Event {
                    Date = DateTimeOffset.Now,
                    Type = "log",
                    Message = entry
                });
            }

            return events;
        }
    }
}