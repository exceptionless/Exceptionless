using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Serialization;

namespace Exceptionless.Ingestion.Load;

internal sealed class StreamingEventContent : HttpContent
{
    private static readonly byte[] _arrayStart = [(byte)'['];
    private static readonly byte[] _arrayEnd = [(byte)']'];
    private static readonly byte[] _comma = [(byte)','];
    private static readonly byte[] _newline = [(byte)'\n'];
    private readonly LoadOptions _options;
    private readonly IngestionProtocol _protocol;
    private readonly string _runMarker;
    private readonly string _corpusName;
    private readonly DateTimeOffset _eventDate;
    private readonly int _start;
    private readonly int _count;

    public StreamingEventContent(LoadOptions options, IngestionProtocol protocol, string runMarker, string signatureNamespace, DateTimeOffset eventDate, int start, int count)
    {
        _options = options;
        _protocol = protocol;
        _runMarker = runMarker;
        _corpusName = signatureNamespace.Replace("-", String.Empty, StringComparison.Ordinal);
        _eventDate = eventDate;
        _start = start;
        _count = count;
        Headers.ContentType = new MediaTypeHeaderValue(protocol is IngestionProtocol.V2 ? "application/json" : "application/x-ndjson");
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
        var transferred = new CountingWriteStream(output);
        await using Stream? compressor = _options.Compression is "gzip"
            ? new GZipStream(transferred, CompressionLevel.Fastest, leaveOpen: true)
            : null;
        var uncompressed = new CountingWriteStream(compressor ?? transferred);

        if (_protocol is IngestionProtocol.V2 && _count > 1)
            await uncompressed.WriteAsync(_arrayStart, cancellationToken);

        for (int offset = 0; offset < _count; offset++)
        {
            if (_protocol is IngestionProtocol.V2 && _count > 1 && offset > 0)
                await uncompressed.WriteAsync(_comma, cancellationToken);

            int index = _start + offset;
            string referenceId = $"{_runMarker}-{index:D8}";
            bool discardedCandidate = index % 100 < _options.DiscardPercent;
            int signature = index % _options.SignatureCardinality;
            string signatureKind = discardedCandidate ? "Discarded" : "Active";
            string? exceptionType = null;
            string? stackTrace = null;
            if (_options.EventType is LoadEventType.Error)
            {
                exceptionType = $"Load.{_corpusName}.{signatureKind}Exception{signature}";
                stackTrace = $"at Load.{_corpusName}.{signatureKind}Service{signature}.Run() in /src/Load.cs:line {signature + 1}";
            }

            if (_protocol is IngestionProtocol.V2)
            {
                var source = new V2LoadEvent(
                    _options.EventType is LoadEventType.Error ? Event.KnownTypes.Error : Event.KnownTypes.Log,
                    _eventDate,
                    _options.Message,
                    referenceId,
                    [_runMarker],
                    _options.EventType is LoadEventType.Error
                        ? new V2LoadEventData(new V2SimpleError(exceptionType!, _options.Message, stackTrace!))
                        : null);
                await JsonSerializer.SerializeAsync(uncompressed, source, LoadJsonContext.Default.V2LoadEvent, cancellationToken);
            }
            else
            {
                var source = new EventIngestionV3Event
                {
                    Id = GetV3ClientId(_runMarker, index),
                    Type = _options.EventType is LoadEventType.Error ? Event.KnownTypes.Error : Event.KnownTypes.Log,
                    Date = _eventDate,
                    Message = _options.Message,
                    ReferenceId = referenceId,
                    Tags = [_runMarker],
                    ExceptionType = exceptionType,
                    StackTrace = stackTrace
                };
                await JsonSerializer.SerializeAsync(uncompressed, source, EventIngestionJsonContext.Default.EventIngestionV3Event, cancellationToken);
                await uncompressed.WriteAsync(_newline, cancellationToken);
            }
        }

        if (_protocol is IngestionProtocol.V2 && _count > 1)
            await uncompressed.WriteAsync(_arrayEnd, cancellationToken);

        await uncompressed.FlushAsync(cancellationToken);
        if (compressor is not null)
            await compressor.DisposeAsync();
        await transferred.FlushAsync(cancellationToken);
        UncompressedBytes = uncompressed.BytesWritten;
        TransferredBytes = transferred.BytesWritten;
    }

    internal static string GetV3ClientId(string runMarker, int index) => $"{runMarker}-{index:D8}-v3";
}

internal sealed record V2LoadEvent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("date")] DateTimeOffset Date,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("reference_id")] string ReferenceId,
    [property: JsonPropertyName("tags")] string[] Tags,
    [property: JsonPropertyName("data"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] V2LoadEventData? Data);

internal sealed record V2LoadEventData(
    [property: JsonPropertyName("@simple_error")] V2SimpleError SimpleError);

internal sealed record V2SimpleError(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("stack_trace")] string StackTrace);

[JsonSerializable(typeof(V2LoadEvent))]
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
