using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Internal
{
    /// <summary>
    /// SelfDiagnosticsEventListener class enables the events from OpenTelemetry event sources
    /// and write the events to an ILoggerFactory.
    /// </summary>
    internal class SelfDiagnosticsEventLogForwarder : EventListener
    {
        private const string EventSourceNamePrefix = "OpenTelemetry-";
        private readonly object lockObj = new object();
        private readonly EventLevel? minEventLevel;
        private readonly List<EventSource> eventSources = new List<EventSource>();
        private readonly ILoggerFactory loggerFactory;
        private readonly ConcurrentDictionary<EventSource, ILogger> loggers = new ConcurrentDictionary<EventSource, ILogger>();

        private readonly Func<EventSourceEvent, Exception, string> formatMessage = FormatMessage;

        internal SelfDiagnosticsEventLogForwarder(ILoggerFactory loggerFactory, EventLevel? minEventLevel = null)
        {
            this.loggerFactory = loggerFactory;
            this.minEventLevel = minEventLevel;

            // set initial levels on existing event sources
            this.SetEventSourceLevels();
        }

        public override void Dispose()
        {
            this.StopForwarding();
            base.Dispose();
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.StartsWith(EventSourceNamePrefix, StringComparison.Ordinal))
            {
                lock (this.lockObj)
                {
                    this.eventSources.Add(eventSource);
                }

                this.SetEventSourceLevel(eventSource);
            }

            base.OnEventSourceCreated(eventSource);
        }

        /// <summary>
        /// This method records the events from event sources to the logging system.
        /// </summary>
        /// <param name="eventData">Data of the EventSource event.</param>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (this.loggerFactory == null)
            {
                return;
            }

            if (!this.loggers.TryGetValue(eventData.EventSource, out var logger))
            {
                logger = this.loggers.GetOrAdd(eventData.EventSource, eventSource => this.loggerFactory.CreateLogger(ToLoggerName(eventSource.Name)));
            }

            logger.Log(MapLevel(eventData.Level), new EventId(eventData.EventId, eventData.EventName), new EventSourceEvent(eventData), null, this.formatMessage);
        }

        private static string ToLoggerName(string name)
        {
            return name.Replace('-', '.');
        }

        private static LogLevel MapLevel(EventLevel level)
        {
            return level switch
            {
                EventLevel.Critical => LogLevel.Critical,
                EventLevel.Error => LogLevel.Error,
                EventLevel.Informational => LogLevel.Information,
                EventLevel.Verbose => LogLevel.Debug,
                EventLevel.Warning => LogLevel.Warning,
                EventLevel.LogAlways => LogLevel.Information,
                _ => LogLevel.None,
            };
        }

        private static string FormatMessage(EventSourceEvent eventSourceEvent, Exception exception)
        {
            return EventSourceEventFormatter.Format(eventSourceEvent.EventData);
        }

        private EventLevel? GetEventLevel(string category)
        {
            return this.minEventLevel;
        }

        private void SetEventSourceLevels()
        {
            lock (this.lockObj)
            {
                foreach (var eventSource in this.eventSources)
                {
                    this.SetEventSourceLevel(eventSource);
                }
            }
        }

        private void StopForwarding()
        {
            lock (this.lockObj)
            {
                foreach (var eventSource in this.eventSources)
                {
                    this.DisableEvents(eventSource);
                }
            }
        }

        private void SetEventSourceLevel(EventSource eventSource)
        {
            var eventLevel = this.GetEventLevel(ToLoggerName(eventSource.Name));

            if (eventLevel.HasValue)
            {
                this.EnableEvents(eventSource, eventLevel.HasValue ? eventLevel.Value : EventLevel.Warning);
            }
            else
            {
                this.DisableEvents(eventSource);
            }
        }

        private readonly struct EventSourceEvent : IReadOnlyList<KeyValuePair<string, object>>
        {
            public EventSourceEvent(EventWrittenEventArgs eventData)
            {
                this.EventData = eventData;
            }

            public EventWrittenEventArgs EventData { get; }

            public int Count => this.EventData.PayloadNames.Count;

            public KeyValuePair<string, object> this[int index] => new KeyValuePair<string, object>(this.EventData.PayloadNames[index], this.EventData.Payload[index]);

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                for (int i = 0; i < this.Count; i++)
                {
                    yield return new KeyValuePair<string, object>(this.EventData.PayloadNames[i], this.EventData.Payload[i]);
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }
    }
}