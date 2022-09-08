using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using Exceptionless.Core.Extensions;

namespace Exceptionless;

public static class AppDiagnostics {
    internal static readonly AssemblyName AssemblyName = typeof(AppDiagnostics).Assembly.GetName();
    internal static readonly string AssemblyVersion = typeof(AppDiagnostics).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? AssemblyName.Version.ToString();
    internal static readonly ActivitySource ActivitySource = new(AssemblyName.Name, AssemblyVersion);
    internal static readonly Meter Meter = new("Exceptionless", AssemblyVersion);
    private static string _metricsPrefix = "exceptionless.";

    private static readonly ConcurrentDictionary<string, Counter<int>> _counters = new();
    private static readonly ConcurrentDictionary<string, GaugeInfo> _gauges = new();
    private static readonly ConcurrentDictionary<string, Histogram<double>> _timers = new();

    public static void Counter(string name, int value = 1) {
        if (!_counters.TryGetValue(_metricsPrefix + name, out var counter))
            counter = _counters.GetOrAdd(_metricsPrefix + name, key => Meter.CreateCounter<int>(key));

        counter.Add(value);
    }

    public static void Gauge(string name, double value) {
        if (!_gauges.TryGetValue(_metricsPrefix + name, out var gauge))
            gauge = _gauges.GetOrAdd(_metricsPrefix + name, key => new GaugeInfo(Meter, key));

        gauge.Value = value;
    }

    public static void Timer(string name, int milliseconds) {
        if (!_timers.TryGetValue(_metricsPrefix + name, out var timer))
            timer = _timers.GetOrAdd(_metricsPrefix + name, key => Meter.CreateHistogram<double>(key, "ms"));

        timer.Record(milliseconds);
    }

    public static IDisposable StartTimer(string name) {
        if (!_timers.TryGetValue(_metricsPrefix + name, out var timer))
            timer = _timers.GetOrAdd(_metricsPrefix + name, key => Meter.CreateHistogram<double>(key, "ms"));

        return timer.StartTimer();
    }

    public static async Task TimeAsync(Func<Task> action, string name) {
        using (StartTimer(name))
            await action().AnyContext();
    }

    public static void Time(Action action, string name) {
        using (StartTimer(name))
            action();
    }

    public static async Task<T> TimeAsync<T>(Func<Task<T>> func, string name) {
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

    internal static readonly Counter<int> EventsSubmitted = Meter.CreateCounter<int>("exceptionless.events.submitted", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> EventsProcessed = Meter.CreateCounter<int>("exceptionless.events.processed", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> EventsProcessingTime = Meter.CreateHistogram<double>("exceptionless.events.processingtime", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> EventsPaidProcessed = Meter.CreateCounter<int>("exceptionless.events.paid.processed", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> EventsProcessErrors = Meter.CreateCounter<int>("exceptionless.events.processing.errors", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> EventsDiscarded = Meter.CreateCounter<int>("exceptionless.events.discarded", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> EventsBlocked = Meter.CreateCounter<int>("exceptionless.events.blocked", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> EventsProcessCancelled = Meter.CreateCounter<int>("exceptionless.events.processing.cancelled", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> EventsRetryCount = Meter.CreateCounter<int>("exceptionless.events.retry.count", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> EventsRetryErrors = Meter.CreateCounter<int>("exceptionless.events.retry.errors", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> EventsFieldCount = Meter.CreateHistogram<double>("exceptionless.events.field.count", description: "Time to get event post", unit: "ms");

    internal static readonly Counter<int> PostsParsed = Meter.CreateCounter<int>("exceptionless.posts.parsed", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> PostsEventCount = Meter.CreateHistogram<double>("exceptionless.posts.eventcount", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> PostsSize = Meter.CreateHistogram<double>("exceptionless.posts.size", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> PostsParseErrors = Meter.CreateCounter<int>("exceptionless.posts.parse.errors", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> PostsMarkFileActiveTime = Meter.CreateHistogram<double>("exceptionless.posts.markfileactivetime", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> PostsParsingTime = Meter.CreateHistogram<double>("exceptionless.posts.parsingtime", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> PostsRetryTime = Meter.CreateHistogram<double>("exceptionless.posts.retrytime", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> PostsAbandonTime = Meter.CreateHistogram<double>("exceptionless.posts.abandontime", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> PostsCompleteTime = Meter.CreateHistogram<double>("exceptionless.posts.completetime", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> PostsDiscarded = Meter.CreateCounter<int>("exceptionless.posts.discarded", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> PostTooBig = Meter.CreateCounter<int>("exceptionless.posts.toobig", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> PostsBlocked = Meter.CreateCounter<int>("exceptionless.posts.blocked", description: "Time to get event post", unit: "ms");

    internal static readonly Histogram<long> PostsMessageSize = Meter.CreateHistogram<long>("exceptionless.posts.message.size", description: "Size of posts", unit: "bytes");
    internal static readonly Histogram<double> PostsCompressedSize = Meter.CreateHistogram<double>("exceptionless.posts.compressed.size", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> PostsUncompressedSize = Meter.CreateHistogram<double>("exceptionless.posts.uncompressed.size", description: "Time to get event post", unit: "ms");
    internal static readonly Histogram<double> PostsDecompressionTime = Meter.CreateHistogram<double>("exceptionless.posts.decompression.time", description: "Time to get event post", unit: "ms");
    internal static readonly Counter<int> PostsDecompressionErrors = Meter.CreateCounter<int>("exceptionless.posts.decompression.errors", description: "Time to get event post", unit: "ms");

    internal static readonly Counter<int> UsageGeocodingApi = Meter.CreateCounter<int>("exceptionless.usage.geocoding", description: "Time to get event post", unit: "ms");
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
