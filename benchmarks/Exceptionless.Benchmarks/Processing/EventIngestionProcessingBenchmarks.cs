using BenchmarkDotNet.Attributes;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Services;

namespace Exceptionless.Benchmarks.Processing;

[MemoryDiagnoser]
public class EventIngestionProcessingBenchmarks
{
    private const string StackTrace = """
        at System.Threading.Tasks.Task.Run()
        at Example.Services.OrderService.Save() in /src/OrderService.cs:line 42
        at Example.Api.OrdersController.Post() in /src/OrdersController.cs:line 18
        """;

    private readonly StackTraceParser _parser = new();
    private StackFingerprintService _fingerprintService = null!;
    private EventMaterializer _materializer = null!;
    private EventIngestionV3Event _source = null!;
    private Organization _organization = null!;
    private Project _project = null!;
    private StackFingerprint _fingerprint = null!;

    [GlobalSetup]
    public void Setup()
    {
        _fingerprintService = new StackFingerprintService(_parser);
        _materializer = new EventMaterializer(_parser, TimeProvider.System);
        _organization = new Organization { Id = "507f191e810c19729de860ea", Name = "Benchmark" };
        _project = new Project
        {
            Id = "507f191e810c19729de860eb",
            OrganizationId = _organization.Id,
            Name = "Benchmark"
        };
        _source = new EventIngestionV3Event
        {
            Id = "benchmark-event-1",
            Type = Event.KnownTypes.Error,
            Message = "Operation failed",
            ExceptionType = "System.InvalidOperationException",
            StackTrace = StackTrace,
            Tags = ["benchmark", "v3"]
        };
        _fingerprint = _fingerprintService.Create(_source, _organization, _project);
    }

    [Benchmark]
    public bool FingerprintRawStack()
    {
        return _parser.TryFindFrame(StackTrace, static frame => frame.DeclaringNamespace?.StartsWith("Example") is true, out _, out _, out _);
    }

    [Benchmark]
    public StackFingerprint CreateFingerprint()
    {
        return _fingerprintService.Create(_source, _organization, _project);
    }

    [Benchmark]
    public int ParseStructuredFrames()
    {
        return _parser.Parse(StackTrace).Count;
    }

    [Benchmark]
    public PersistentEvent MaterializeSurvivor()
    {
        return _materializer.Materialize(_source, _fingerprint, _organization, _project);
    }
}
