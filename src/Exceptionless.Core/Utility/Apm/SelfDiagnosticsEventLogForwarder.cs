using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Internal;

/// <summary>
/// SelfDiagnosticsEventListener class enables the events from OpenTelemetry event sources
/// and write the events to an ILoggerFactory.
/// </summary>
internal class SelfDiagnosticsEventLogForwarder : EventListener
{
    private const string EventSourceNamePrefix = "OpenTelemetry-";
    private readonly object lockObj = new();
    private readonly EventLevel? minEventLevel;
    private readonly List<EventSource> eventSources = new();
    private readonly ILoggerFactory loggerFactory;
    private readonly ConcurrentDictionary<EventSource, ILogger> loggers = new();

    private readonly Func<EventSourceEvent, Exception, string> formatMessage = FormatMessage;

    internal SelfDiagnosticsEventLogForwarder(ILoggerFactory loggerFactory, EventLevel? minEventLevel = null)
    {
        this.loggerFactory = loggerFactory;
        this.minEventLevel = minEventLevel;

        // set initial levels on existing event sources
        SetEventSourceLevels();
    }

    public override void Dispose()
    {
        StopForwarding();
        base.Dispose();
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name.StartsWith(EventSourceNamePrefix, StringComparison.Ordinal))
        {
            lock (lockObj)
            {
                eventSources.Add(eventSource);
            }

            SetEventSourceLevel(eventSource);
        }

        base.OnEventSourceCreated(eventSource);
    }

    /// <summary>
    /// This method records the events from event sources to the logging system.
    /// </summary>
    /// <param name="eventData">Data of the EventSource event.</param>
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (loggerFactory is null)
        {
            return;
        }

        if (!loggers.TryGetValue(eventData.EventSource, out var logger))
        {
            logger = loggers.GetOrAdd(eventData.EventSource, eventSource => loggerFactory.CreateLogger(ToLoggerName(eventSource.Name)));
        }

        logger.Log(MapLevel(eventData.Level), new EventId(eventData.EventId, eventData.EventName), new EventSourceEvent(eventData), null, formatMessage!);
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
        return minEventLevel;
    }

    private void SetEventSourceLevels()
    {
        lock (lockObj)
        {
            foreach (var eventSource in eventSources)
            {
                SetEventSourceLevel(eventSource);
            }
        }
    }

    private void StopForwarding()
    {
        lock (lockObj)
        {
            foreach (var eventSource in eventSources)
            {
                DisableEvents(eventSource);
            }
        }
    }

    private void SetEventSourceLevel(EventSource eventSource)
    {
        var eventLevel = GetEventLevel(ToLoggerName(eventSource.Name));

        if (eventLevel.HasValue)
        {
            EnableEvents(eventSource, eventLevel.HasValue ? eventLevel.Value : EventLevel.Warning);
        }
        else
        {
            DisableEvents(eventSource);
        }
    }

    private readonly struct EventSourceEvent : IReadOnlyList<KeyValuePair<string, object?>>
    {
        public EventSourceEvent(EventWrittenEventArgs eventData)
        {
            EventData = eventData;
        }

        public EventWrittenEventArgs EventData { get; }

        public int Count => EventData.PayloadNames?.Count ?? 0;

        public KeyValuePair<string, object?> this[int index]
        {
            get
            {
                if (EventData.PayloadNames is [..] collection && collection.Count >= index)
                {
                    return new(EventData.PayloadNames[index], EventData.Payload?[index]);
                }

                throw new IndexOutOfRangeException();
            }
        }

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
            {
                yield return new KeyValuePair<string, object?>(EventData.PayloadNames![i], EventData.Payload?[i]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
