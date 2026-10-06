using System.Buffers;
using System.IO.Pipelines;
using System.Text.Json;
using Exceptionless.Core.Models.Ingestion;

namespace Exceptionless.Web.Utility;

/// <summary>
/// Reads V3 events from a request body one at a time without buffering the body. The body may be
/// a single JSON object, a JSON array of objects, or whitespace-separated objects such as
/// newline-delimited JSON. Event boundaries come from the JSON structure, so pretty-printed events
/// work. Each event is size limited before it is deserialized. An event that is too large, is not
/// an object, or does not match the event shape is reported and skipped. Invalid JSON outside an
/// array is reported and skipped by resuming at the next line that starts with <c>{</c>.
/// </summary>
internal sealed class EventIngestionV3StreamReader
{
    private static readonly JsonReaderOptions _scanOptions = new() { MaxDepth = 64 };

    private readonly PipeReader _reader;
    private readonly long _maximumEventSize;
    private readonly JsonSerializerOptions _serializerOptions;

    private Framing _framing;
    private ArrayPosition _arrayPosition;
    private bool _checkedByteOrderMark;
    private int _nextIndex;

    private bool _inValue;
    private bool _skippingValue;
    private long _valueBytesScanned;
    private JsonReaderState _valueState;

    private bool _resynchronizing;
    private bool _resynchronizingAtLineStart;

    public EventIngestionV3StreamReader(PipeReader reader, long maximumEventSize, JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEventSize);
        ArgumentNullException.ThrowIfNull(serializerOptions);

        _reader = reader;
        _maximumEventSize = maximumEventSize;
        _serializerOptions = serializerOptions;
    }

    /// <summary>
    /// Returns the next event, or <see langword="null"/> at the end of the body.
    /// </summary>
    /// <exception cref="JsonException">The body is a JSON array that is malformed.</exception>
    public async ValueTask<EventIngestionV3StreamRecord?> ReadAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            ReadResult result = await _reader.ReadAsync(cancellationToken);
            ReadOnlySequence<byte> buffer = result.Buffer;
            EventIngestionV3StreamRecord? record;
            try
            {
                record = TryRead(ref buffer, result.IsCompleted);
            }
            catch
            {
                _reader.AdvanceTo(buffer.Start, buffer.End);
                throw;
            }

            if (record is not null)
            {
                _reader.AdvanceTo(buffer.Start);
                return record;
            }

            if (result.IsCompleted)
            {
                _reader.AdvanceTo(buffer.End);
                return null;
            }

            _reader.AdvanceTo(buffer.Start, buffer.End);
        }
    }

    private EventIngestionV3StreamRecord? TryRead(ref ReadOnlySequence<byte> buffer, bool isCompleted)
    {
        while (true)
        {
            if (_resynchronizing)
            {
                if (!TryResynchronize(ref buffer))
                {
                    return null;
                }

                _resynchronizing = false;
            }

            if (_inValue)
            {
                return ContinueValue(ref buffer, isCompleted);
            }

            if (!_checkedByteOrderMark)
            {
                if (buffer.Length < 3 && !isCompleted && buffer.FirstSpan is [0xEF, ..])
                {
                    return null;
                }

                if (buffer.Length >= 3 && buffer.FirstSpan.Length >= 3 && buffer.FirstSpan[..3].SequenceEqual("﻿"u8))
                {
                    buffer = buffer.Slice(3);
                }

                _checkedByteOrderMark = true;
            }

            SkipWhitespace(ref buffer);
            if (buffer.IsEmpty)
            {
                if (isCompleted && _framing is Framing.Array && _arrayPosition is not ArrayPosition.Ended)
                {
                    throw new JsonException("The request ended before the JSON array was closed.");
                }

                return null;
            }

            byte next = buffer.FirstSpan[0];
            if (_framing is Framing.Unknown)
            {
                if (next == (byte)'[')
                {
                    _framing = Framing.Array;
                    _arrayPosition = ArrayPosition.ValueOrEnd;
                    buffer = buffer.Slice(1);
                    continue;
                }

                _framing = Framing.Sequence;
            }
            else if (_framing is Framing.Array)
            {
                switch (_arrayPosition)
                {
                    case ArrayPosition.Ended:
                        throw new JsonException("The request contains data after the end of the JSON array.");
                    case ArrayPosition.ValueOrEnd when next == (byte)']':
                    case ArrayPosition.SeparatorOrEnd when next == (byte)']':
                        _arrayPosition = ArrayPosition.Ended;
                        buffer = buffer.Slice(1);
                        continue;
                    case ArrayPosition.SeparatorOrEnd when next == (byte)',':
                        _arrayPosition = ArrayPosition.Value;
                        buffer = buffer.Slice(1);
                        continue;
                    case ArrayPosition.SeparatorOrEnd:
                        throw new JsonException("Expected ',' or ']' after an event in the JSON array.");
                    case ArrayPosition.Value when next == (byte)']':
                        throw new JsonException("The JSON array has a trailing comma.");
                }
            }

            _inValue = true;
            _skippingValue = false;
            _valueBytesScanned = 0;
            _valueState = new JsonReaderState(_scanOptions);
        }
    }

    private EventIngestionV3StreamRecord? ContinueValue(ref ReadOnlySequence<byte> buffer, bool isCompleted)
    {
        // Until a value is complete its bytes stay in the pipe so it can be deserialized without a
        // copy. A value over the size limit is scanned to its end without being retained.
        ReadOnlySequence<byte> unscanned = _skippingValue ? buffer : buffer.Slice(_valueBytesScanned);
        var scanner = new Utf8JsonReader(unscanned, isCompleted, _valueState);
        bool isComplete = false;
        try
        {
            while (scanner.Read())
            {
                if (scanner.CurrentDepth == 0 && scanner.TokenType is not (JsonTokenType.StartObject or JsonTokenType.StartArray))
                {
                    isComplete = true;
                    break;
                }
            }
        }
        catch (JsonException ex)
        {
            if (_framing is Framing.Array)
            {
                throw;
            }

            // Skip the malformed value and resume at the next line that starts a new object.
            _inValue = false;
            _resynchronizing = true;
            _resynchronizingAtLineStart = false;
            return Invalid(_valueBytesScanned + scanner.BytesConsumed, EventIngestionV3ErrorCodes.InvalidJson, $"The event is not valid JSON: {ex.Message}");
        }

        long scannedThisRead = scanner.BytesConsumed;
        if (!isComplete)
        {
            _valueState = scanner.CurrentState;
            if (_skippingValue)
            {
                buffer = buffer.Slice(scannedThisRead);
            }
            else if (buffer.Length > _maximumEventSize)
            {
                buffer = buffer.Slice(_valueBytesScanned + scannedThisRead);
                _skippingValue = true;
            }

            _valueBytesScanned += scannedThisRead;
            return null;
        }

        _inValue = false;
        if (_framing is Framing.Array)
        {
            _arrayPosition = ArrayPosition.SeparatorOrEnd;
        }

        long size = _valueBytesScanned + scannedThisRead;
        if (_skippingValue || size > _maximumEventSize)
        {
            buffer = buffer.Slice(_skippingValue ? scannedThisRead : size);
            return Invalid(size, EventIngestionV3ErrorCodes.EventTooLarge, $"The event is larger than the maximum event size of {_maximumEventSize} bytes.");
        }

        ReadOnlySequence<byte> value = buffer.Slice(0, size);
        buffer = buffer.Slice(size);
        if (value.FirstSpan[0] != (byte)'{')
        {
            return Invalid(size, EventIngestionV3ErrorCodes.InvalidEvent, "Each event must be a JSON object.");
        }

        try
        {
            var valueReader = new Utf8JsonReader(value, new JsonReaderOptions { MaxDepth = _scanOptions.MaxDepth });
            var ev = JsonSerializer.Deserialize<EventIngestionV3Event>(ref valueReader, _serializerOptions);
            return ev is null
                ? Invalid(size, EventIngestionV3ErrorCodes.InvalidEvent, "Each event must be a JSON object.")
                : new EventIngestionV3StreamRecord(_nextIndex++, size, ev, null, null);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or FormatException or OverflowException)
        {
            return Invalid(size, EventIngestionV3ErrorCodes.InvalidEvent, $"The event does not match the event format: {ex.Message}");
        }
    }

    private EventIngestionV3StreamRecord Invalid(long size, string code, string message)
    {
        return new EventIngestionV3StreamRecord(_nextIndex++, size, null, code, message);
    }

    private bool TryResynchronize(ref ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);
        while (reader.TryRead(out byte value))
        {
            if (value == (byte)'\n')
            {
                _resynchronizingAtLineStart = true;
                continue;
            }

            if (!_resynchronizingAtLineStart || value is (byte)' ' or (byte)'\t' or (byte)'\r')
            {
                continue;
            }

            if (value == (byte)'{')
            {
                reader.Rewind(1);
                buffer = buffer.Slice(reader.Position);
                return true;
            }

            _resynchronizingAtLineStart = false;
        }

        buffer = buffer.Slice(buffer.End);
        return false;
    }

    private static void SkipWhitespace(ref ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);
        reader.AdvancePastAny((byte)' ', (byte)'\t', (byte)'\r', (byte)'\n');
        buffer = buffer.Slice(reader.Position);
    }

    private enum Framing
    {
        Unknown,
        Sequence,
        Array
    }

    private enum ArrayPosition
    {
        ValueOrEnd,
        Value,
        SeparatorOrEnd,
        Ended
    }
}

/// <summary>
/// One event read from a V3 request. Either <see cref="Event"/> or <see cref="ErrorCode"/> is set.
/// </summary>
internal readonly record struct EventIngestionV3StreamRecord(int Index, long Size, EventIngestionV3Event? Event, string? ErrorCode, string? ErrorMessage);
