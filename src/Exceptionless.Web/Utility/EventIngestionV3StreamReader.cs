using System.Buffers;
using System.IO.Pipelines;
using System.Text.Json;
using Exceptionless.Core.Models.Ingestion;

namespace Exceptionless.Web.Utility;

/// <summary>
/// Frames newline-delimited JSON before materialization so a single record can never cause an
/// unbounded allocation. It projects routing fields first and materializes only surviving records.
/// </summary>
internal static class EventIngestionV3StreamReader
{
    private static readonly JsonReaderOptions _readerOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = EventIngestionV3Limits.MaximumJsonDepth
    };

    public static async ValueTask<EventIngestionV3StreamRecord?> ReadAsync(
        PipeReader pipeReader,
        long maximumEventSize,
        CancellationToken cancellationToken,
        MemoryPool<byte>? memoryPool = null)
    {
        ArgumentNullException.ThrowIfNull(pipeReader);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEventSize);
        maximumEventSize = Math.Min(maximumEventSize, Array.MaxLength);

        // PipeReader retains an incomplete record between reads. Remember how much of that record
        // has already been searched so a client that fragments a large line into tiny writes cannot
        // make us rescan the prefix on every read (quadratic work).
        long examinedLength = 0;
        bool objectPrefixValidated = false;
        while (true)
        {
            ReadResult readResult = await pipeReader.ReadAsync(cancellationToken);
            ReadOnlySequence<byte> buffer = readResult.Buffer;
            ReadOnlySequence<byte> remaining = buffer;

            try
            {
                while (true)
                {
                    if (examinedLength > remaining.Length)
                    {
                        throw new InvalidOperationException("The ingestion pipe returned less data than it retained.");
                    }

                    ReadOnlySequence<byte> unexamined = remaining.Slice(examinedLength);
                    if (unexamined.PositionOf((byte)'\n') is not { } newline)
                    {
                        break;
                    }

                    ReadOnlySequence<byte> record = remaining.Slice(0, newline);
                    SequencePosition consumed = remaining.GetPosition(1, newline);
                    if (record.Length > maximumEventSize)
                    {
                        throw new EventIngestionV3RecordTooLargeException();
                    }

                    if (!objectPrefixValidated)
                    {
                        objectPrefixValidated = EnsureObjectPrefix(unexamined.Slice(0, newline));
                    }

                    if (IsJsonWhitespace(record))
                    {
                        remaining = remaining.Slice(consumed);
                        examinedLength = 0;
                        objectPrefixValidated = false;
                        continue;
                    }

                    EventIngestionV3BufferedRecord bufferedRecord = Buffer(record, memoryPool ?? MemoryPool<byte>.Shared);
                    pipeReader.AdvanceTo(consumed, consumed);
                    return new EventIngestionV3StreamRecord(bufferedRecord);
                }

                ReadOnlySequence<byte> newlyExamined = remaining.Slice(examinedLength);
                if (!objectPrefixValidated)
                {
                    objectPrefixValidated = EnsureObjectPrefix(newlyExamined);
                }

                if (remaining.Length > maximumEventSize)
                {
                    throw new EventIngestionV3RecordTooLargeException();
                }

                if (readResult.IsCompleted)
                {
                    if (IsJsonWhitespace(remaining))
                    {
                        pipeReader.AdvanceTo(buffer.End, buffer.End);
                        return null;
                    }

                    EventIngestionV3BufferedRecord bufferedRecord = Buffer(remaining, memoryPool ?? MemoryPool<byte>.Shared);
                    pipeReader.AdvanceTo(buffer.End, buffer.End);
                    return new EventIngestionV3StreamRecord(bufferedRecord);
                }

                examinedLength = remaining.Length;
                pipeReader.AdvanceTo(remaining.Start, buffer.End);
            }
            catch
            {
                pipeReader.AdvanceTo(buffer.Start, buffer.End);
                throw;
            }
        }
    }

    private static EventIngestionV3BufferedRecord Buffer(ReadOnlySequence<byte> record, MemoryPool<byte> memoryPool)
    {
        int length = checked((int)record.Length);
        IMemoryOwner<byte> owner = memoryPool.Rent(length);
        try
        {
            record.CopyTo(owner.Memory.Span);
            EventIngestionV3Event routingEvent = ParseRoutingEvent(owner.Memory.Span[..length]);
            return new EventIngestionV3BufferedRecord(owner, length, routingEvent);
        }
        catch
        {
            owner.Dispose();
            throw;
        }
    }

    private static EventIngestionV3Event ParseRoutingEvent(ReadOnlySpan<byte> record)
    {
        var jsonReader = new Utf8JsonReader(record, isFinalBlock: true, new JsonReaderState(_readerOptions));
        if (!jsonReader.Read() || jsonReader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Each NDJSON line must contain one event object.");
        }

        string? id = null;
        string? type = null;
        string? source = null;
        string? exceptionType = null;
        string? stackTrace = null;
        EventIngestionV3Stacking? stacking = null;
        while (jsonReader.Read() && jsonReader.TokenType != JsonTokenType.EndObject)
        {
            if (jsonReader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("The event record must be a JSON object.");
            }

            bool isId = jsonReader.ValueTextEquals("id"u8);
            bool isType = jsonReader.ValueTextEquals("type"u8);
            bool isSource = jsonReader.ValueTextEquals("source"u8);
            bool isExceptionType = jsonReader.ValueTextEquals("exception_type"u8);
            bool isStackTrace = jsonReader.ValueTextEquals("stack_trace"u8);
            bool isStacking = jsonReader.ValueTextEquals("stacking"u8);
            if (!jsonReader.Read())
            {
                throw new JsonException("The event record ended before a property value was complete.");
            }

            if (isId)
            {
                id = ReadNullableString(ref jsonReader);
            }
            else if (isType)
            {
                type = ReadNullableString(ref jsonReader);
            }
            else if (isSource)
            {
                source = ReadNullableString(ref jsonReader);
            }
            else if (isExceptionType)
            {
                exceptionType = ReadNullableString(ref jsonReader);
            }
            else if (isStackTrace)
            {
                stackTrace = ReadNullableString(ref jsonReader);
            }
            else if (isStacking)
            {
                stacking = ReadStacking(ref jsonReader);
            }
            else
            {
                jsonReader.Skip();
            }
        }

        if (jsonReader.TokenType != JsonTokenType.EndObject || jsonReader.Read())
        {
            throw new JsonException("Each NDJSON line must contain exactly one event object.");
        }

        return new EventIngestionV3Event
        {
            Id = id!,
            Type = type!,
            Source = source,
            ExceptionType = exceptionType,
            StackTrace = stackTrace,
            Stacking = stacking
        };
    }

    private static EventIngestionV3Stacking? ReadStacking(ref Utf8JsonReader jsonReader)
    {
        if (jsonReader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (jsonReader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("stacking must be a JSON object.");
        }

        Dictionary<string, string>? signatureData = null;
        while (jsonReader.Read() && jsonReader.TokenType != JsonTokenType.EndObject)
        {
            if (jsonReader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("stacking must be a JSON object.");
            }

            bool isSignatureData = jsonReader.ValueTextEquals("signature_data"u8);
            if (!jsonReader.Read())
            {
                throw new JsonException("stacking ended before a property value was complete.");
            }

            if (isSignatureData)
            {
                signatureData = ReadSignatureData(ref jsonReader);
            }
            else
            {
                jsonReader.Skip();
            }
        }

        if (jsonReader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException("stacking must be a complete JSON object.");
        }

        return new EventIngestionV3Stacking
        {
            SignatureData = signatureData!
        };
    }

    private static Dictionary<string, string>? ReadSignatureData(ref Utf8JsonReader jsonReader)
    {
        if (jsonReader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (jsonReader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("stacking.signature_data must be a JSON object.");
        }

        var values = new Dictionary<string, string>();
        while (jsonReader.Read() && jsonReader.TokenType != JsonTokenType.EndObject)
        {
            if (jsonReader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("stacking.signature_data must be a JSON object.");
            }

            string key = jsonReader.GetString()!;
            if (!jsonReader.Read())
            {
                throw new JsonException("stacking.signature_data ended before a value was complete.");
            }

            values[key] = ReadNullableString(ref jsonReader)!;
        }

        if (jsonReader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException("stacking.signature_data must be a complete JSON object.");
        }

        return values;
    }

    private static string? ReadNullableString(ref Utf8JsonReader jsonReader)
    {
        if (jsonReader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (jsonReader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("The event property must be a JSON string or null.");
        }

        return jsonReader.GetString();
    }

    private static bool IsJsonWhitespace(ReadOnlySequence<byte> value)
    {
        foreach (ReadOnlyMemory<byte> segment in value)
        {
            foreach (byte item in segment.Span)
            {
                if (item is not (0x20 or 0x09 or 0x0A or 0x0D))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool EnsureObjectPrefix(ReadOnlySequence<byte> value)
    {
        foreach (ReadOnlyMemory<byte> segment in value)
        {
            foreach (byte item in segment.Span)
            {
                if (item is 0x20 or 0x09 or 0x0A or 0x0D)
                {
                    continue;
                }

                if (item != (byte)'{')
                {
                    throw new JsonException("Each NDJSON line must contain one event object.");
                }

                return true;
            }
        }

        return false;
    }
}

internal readonly record struct EventIngestionV3StreamRecord(EventIngestionV3BufferedRecord BufferedRecord)
{
    public EventIngestionV3Event Event => BufferedRecord.RoutingEvent;
    public long Size => BufferedRecord.Length;
}

internal sealed class EventIngestionV3RecordTooLargeException : Exception;
