using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeSmith.Core.Component;
using CodeSmith.Core.Extensions;
using CodeSmith.Core.Scheduler;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Queues;
using Exceptionless.Json;
using Exceptionless.Models;
using NLog.Fluent;
using RazorEngine;
using Encoding = System.Text.Encoding;

namespace Exceptionless.Core.Jobs {
    public class ProcessEventsJob : JobBase {
        private readonly IQueue<EventPost> _queue;
        private readonly EventPipeline _eventPipeline;

        public ProcessEventsJob(IQueue<EventPost> queue, EventPipeline eventPipeline) {
            _queue = queue;
            _eventPipeline = eventPipeline;
        }

        public override JobResult Run(JobContext context) {
            Log.Info().Message("Process events job starting").Write();

            var task = _queue.DequeueAsync().ContinueWith(t => {
                if (t.IsFaulted || t.Result == null || t.Result.Value == null) {
                    const string message = "An error occurred while trying to dequeue the EventPost.";
                    Log.Error().Exception(t.Exception).Message(message).Write();
                    return TaskHelper.FromError(t.Exception ?? new Exception(message));
                }

                Log.Info().Message("Processing EventPost '{0}'.", t.Result.Id).Write();

                List<Event> events;
                try {
                    events = ProcessEventPost(t.Result.Value);
                } catch (Exception ex) {
                    t.Result.AbandonAsync().Wait();

                    // TODO: Add the EventPost to the logged exception.
                    Log.Error().Exception(ex).Message("An error occurred while processing the EventPost '{0}'.", t.Result.Id).Write();
                    return TaskHelper.FromError(t.Exception);
                }

                bool isSingleEvent = events.Count == 1;
                int errorCount = 0;
                foreach (Event ev in events) {
                    try {
                        _eventPipeline.Run(ev);
                    } catch (Exception ex) {
                        Log.Error().Exception(ex).Message("An error occurred while processing the EventPipeline.").Write();

                        if (!isSingleEvent) {
                            // Put this single event back into the queue so we can retry it separately.
                            _queue.EnqueueAsync(new EventPost {
                                Data = null, // ev,
                                ProjectId = ev.ProjectId,
                                ContentEncoding = t.Result.Value.ContentEncoding,
                                ContentType = t.Result.Value.ContentType,
                            });
                        }

                        errorCount++;
                    }
                }

                if (isSingleEvent && errorCount > 0)
                    t.Result.AbandonAsync().Wait();
                else
                    t.Result.CompleteAsync().Wait();

                return t;
            });

            return !task.IsFaulted 
                ? new JobResult { Result = "Successfully processed the EventData" } 
                : new JobResult { Cancelled = task.IsCanceled, Error = task.Exception, Result = "An error occurred while processing the EventData"};
        }

        private List<Event> ProcessEventPost(EventPost ep) {
            byte[] data = String.Equals(ep.ContentEncoding, "gzip", StringComparison.InvariantCultureIgnoreCase) ? ep.Data.Decompress() : ep.Data;
            
            // TODO: cache this.
            var encoding = Encoding.UTF8;
            if (!String.IsNullOrEmpty(ep.ContentType)) {
                var encodingInfo = Encoding.GetEncodings().FirstOrDefault(info => info.Name == ep.ContentType);
                if (encodingInfo != null)
                    encoding = encodingInfo.GetEncoding();
            }

            var events = new List<Event>();
            //projectid

            string result = encoding.GetString(data);
            switch (result.GetJsonType()) {
                case JsonType.None:
                    foreach (var entry in result.SplitAndTrim(new[] { Environment.NewLine }))
                        events.Add(new Event { Date = DateTimeOffset.Now, Type = "log", Message = entry });

                    break;
                case JsonType.Object:
                    events.Add(JsonConvert.DeserializeObject<Event>(result));
                    break;
                case JsonType.Array:
                    events.AddRange(JsonConvert.DeserializeObject<Event[]>(result));
                    break;
            }

            return events;
        } 
    }
}