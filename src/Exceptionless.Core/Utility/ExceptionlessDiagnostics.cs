using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using Exceptionless.Core.Extensions;

namespace Exceptionless;

internal static class ExceptionlessDiagnostics {
    internal static readonly AssemblyName AssemblyName = typeof(ExceptionlessDiagnostics).Assembly.GetName();
    internal static readonly string AssemblyVersion = typeof(ExceptionlessDiagnostics).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? AssemblyName.Version.ToString();
    internal static readonly ActivitySource ActivitySource = new(AssemblyName.Name, AssemblyVersion);
    internal static readonly Meter Meter = new("Exceptionless", AssemblyVersion);

    private static readonly ConcurrentDictionary<string, Counter<int>> _counters = new();
    private static readonly ConcurrentDictionary<string, GaugeInfo> _gauges = new();
    private static readonly ConcurrentDictionary<string, Histogram<double>> _timers = new();

    internal static void Counter(string name, int value = 1) {
        var counter = _counters.GetOrAdd(name, Meter.CreateCounter<int>(name));
        counter.Add(value);
    }

    internal static void Gauge(string name, double value) {
        var gauge = _gauges.GetOrAdd(name, new GaugeInfo(Meter, name));
        gauge.Value = value;
    }

    internal static void Timer(string name, int milliseconds) {
        var timer = _timers.GetOrAdd(name, Meter.CreateHistogram<double>(name, "ms"));
        timer.Record(milliseconds);
    }

    internal static IDisposable StartTimer(string name) {
        var timer = _timers.GetOrAdd(name, Meter.CreateHistogram<double>(name, "ms"));
        return timer.StartTimer();
    }

    internal static async Task TimeAsync(Func<Task> action, string name) {
        using (StartTimer(name))
            await action().AnyContext();
    }

    internal static void Time(Action action, string name) {
        using (StartTimer(name))
            action();
    }

    internal static async Task<T> TimeAsync<T>(Func<Task<T>> func, string name) {
        using (StartTimer(name))
            return await func().AnyContext();
    }

    private class GaugeInfo {
        public GaugeInfo(Meter meter, string name) {
            Gauge = meter.CreateObservableGauge(name, () => Value);
        }

        public ObservableGauge<double> Gauge { get; }
        public double Value { get; set; }
    }

    internal static readonly Counter<int> EventsSubmitted = Meter.CreateCounter<int>("events.submitted", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> EventsProcessed = Meter.CreateCounter<int>("events.processed", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> EventsProcessingTime = Meter.CreateHistogram<double>("events.processingtime", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> EventsPaidProcessed = Meter.CreateCounter<int>("events.paid.processed", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> EventsProcessErrors = Meter.CreateCounter<int>("events.processing.errors", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> EventsDiscarded = Meter.CreateCounter<int>("events.discarded", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> EventsProcessCancelled = Meter.CreateCounter<int>("events.processing.cancelled", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> EventsRetryCount = Meter.CreateCounter<int>("events.retry.count", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> EventsRetryErrors = Meter.CreateCounter<int>("events.retry.errors", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> EventsFieldCount = Meter.CreateHistogram<double>("events.field.count", description: "Time to get event post", unit: "ms");

    internal static readonly Counter<int> PostsParsed = Meter.CreateCounter<int>("posts.parsed", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> PostsEventCount = Meter.CreateHistogram<double>("posts.eventcount", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> PostsSize = Meter.CreateHistogram<double>("posts.size", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> PostsParseErrors = Meter.CreateCounter<int>("posts.parse.errors", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> PostsMarkFileActiveTime = Meter.CreateHistogram<double>("posts.markfileactivetime", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> PostsParsingTime = Meter.CreateHistogram<double>("posts.parsingtime", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> PostsRetryTime = Meter.CreateHistogram<double>("posts.retrytime", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> PostsAbandonTime = Meter.CreateHistogram<double>("posts.abandontime", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> PostsCompleteTime = Meter.CreateHistogram<double>("posts.completetime", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> PostsDiscarded = Meter.CreateCounter<int>("posts.discarded", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> PostsBlocked = Meter.CreateCounter<int>("posts.blocked", description: "Time to get event post", unit: "ms");

    internal static readonly Histogram<long> PostsMessageSize = Meter.CreateHistogram<long>("posts.message.size", description: "Size of posts", unit: "bytes");
    internal static readonly Histogram<double> PostsCompressedSize = Meter.CreateHistogram<double>("posts.compressed.size", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> PostsUncompressedSize = Meter.CreateHistogram<double>("posts.uncompressed.size", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> PostsDecompressionTime = Meter.CreateHistogram<double>("posts.decompression.time", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> PostsDecompressionErrors = Meter.CreateCounter<int>("posts.decompression.errors", description: "Time to get event post", unit: "ms");

    internal static readonly Counter<int> UsageGeocodingApi = Meter.CreateCounter<int>("usage.geocoding", description: "Time to get event post", unit: "ms");
}

public static class MetricsClientExtensions {
    public static IDisposable StartTimer(this Histogram<double> histogram) {
        return new HistogramTimer(histogram);
    }

    public static async Task TimeAsync(this Histogram<double> histogram, Func<Task> action) {
        using (histogram.StartTimer())
            await action().AnyContext();
    }

    public static void Time(this Histogram<double> histogram, Action action) {
        using (histogram.StartTimer())
            action();
    }

    public static async Task<T> TimeAsync<T>(this Histogram<double> histogram, Func<Task<T>> func) {
        using (histogram.StartTimer())
            return await func().AnyContext();
    }
}

public class HistogramTimer : IDisposable {
    private readonly Stopwatch _stopWatch;
    private bool _disposed;
    private readonly Histogram<double> _histogram;

    public HistogramTimer(Histogram<double> histogram) {
        _histogram = histogram;
        _stopWatch = Stopwatch.StartNew();
    }

    public void Dispose() {
        if (_disposed)
            return;

        _disposed = true;
        _stopWatch.Stop();
        _histogram.Record((int)_stopWatch.ElapsedMilliseconds);
    }
}
