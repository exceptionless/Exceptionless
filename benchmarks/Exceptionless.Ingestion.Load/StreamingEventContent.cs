using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exceptionless.Ingestion.Load;

internal sealed class StreamingEventContent : HttpContent
{
    private const string ErrorEventType = "error";
    private const string LogEventType = "log";
    private static readonly byte[] _arrayStart = [(byte)'['];
    private static readonly byte[] _arrayEnd = [(byte)']'];
    private static readonly byte[] _comma = [(byte)','];
    private readonly LoadOptions _options;
    private readonly string _runMarker;
    private readonly string _corpusName;
    private readonly DateTimeOffset _eventDate;
    private readonly int _start;
    private readonly int _count;

    public StreamingEventContent(LoadOptions options, string runMarker, string signatureNamespace, DateTimeOffset eventDate, int start, int count)
    {
        _options = options;
        _runMarker = runMarker;
        _corpusName = signatureNamespace.Replace("-", String.Empty, StringComparison.Ordinal);
        _eventDate = eventDate;
        _start = start;
        _count = count;
        Headers.ContentType = new MediaTypeHeaderValue("application/json");
        if (options.Compression is "gzip")
            Headers.ContentEncoding.Add(options.Compression);
    }

    public long UncompressedBytes { get; private set; }
    public long TransferredBytes { get; private set; }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => WriteAsync(stream, CancellationToken.None);

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) => WriteAsync(stream, cancellationToken);

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }

    private async Task WriteAsync(Stream output, CancellationToken cancellationToken)
    {
        // --batch-size 1 posts a single JSON object; any larger batch size posts a JSON array, including a short final request.
        bool writeArray = _options.BatchSize > 1;
        var transferred = new CountingWriteStream(output);
        await using Stream? compressor = _options.Compression is "gzip"
            ? new GZipStream(transferred, CompressionLevel.Fastest, leaveOpen: true)
            : null;
        var uncompressed = new CountingWriteStream(compressor ?? transferred);

        if (writeArray)
            await uncompressed.WriteAsync(_arrayStart, cancellationToken);

        for (int offset = 0; offset < _count; offset++)
        {
            if (writeArray && offset > 0)
                await uncompressed.WriteAsync(_comma, cancellationToken);

            int index = _start + offset;
            bool discardedCandidate = index % 100 < _options.DiscardPercent;
            int signature = index % _options.SignatureCardinality;
            string signatureKind = discardedCandidate ? "Discarded" : "Active";
            LoadEventData? data = null;
            if (_options.EventType is LoadEventType.Error)
            {
                string exceptionType = $"Load.{_corpusName}.{signatureKind}Exception{signature}";
                string stackTrace = $"at Load.{_corpusName}.{signatureKind}Service{signature}.Run() in /src/Load.cs:line {signature + 1}";
                data = new LoadEventData(new LoadSimpleError(exceptionType, _options.Message, stackTrace));
            }

            var source = new LoadEvent(
                _options.EventType is LoadEventType.Error ? ErrorEventType : LogEventType,
                _eventDate,
                _options.Message,
                $"{_runMarker}-{index:D8}",
                [_runMarker],
                data);
            await JsonSerializer.SerializeAsync(uncompressed, source, LoadJsonContext.Default.LoadEvent, cancellationToken);
        }

        if (writeArray)
            await uncompressed.WriteAsync(_arrayEnd, cancellationToken);

        await uncompressed.FlushAsync(cancellationToken);
        if (compressor is not null)
            await compressor.DisposeAsync();
        await transferred.FlushAsync(cancellationToken);
        UncompressedBytes = uncompressed.BytesWritten;
        TransferredBytes = transferred.BytesWritten;
    }
}

internal sealed record LoadEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("date")] DateTimeOffset Date,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("reference_id")] string ReferenceId,
    [property: JsonPropertyName("tags")] string[] Tags,
    [property: JsonPropertyName("data"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LoadEventData? Data);

internal sealed record LoadEventData(
    [property: JsonPropertyName("@simple_error")] LoadSimpleError SimpleError);

internal sealed record LoadSimpleError(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("stack_trace")] string StackTrace);

[JsonSerializable(typeof(LoadEvent))]
internal sealed partial class LoadJsonContext : JsonSerializerContext;

internal sealed class CountingWriteStream(Stream inner) : Stream
{
    public long BytesWritten { get; private set; }
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => inner.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count)
    {
        inner.Write(buffer, offset, count);
        BytesWritten += count;
    }
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await inner.WriteAsync(buffer, cancellationToken);
        BytesWritten += buffer.Length;
    }
}
