using Exceptionless.Core.Services;
using HttpBadHttpRequestException = Microsoft.AspNetCore.Http.BadHttpRequestException;

namespace Exceptionless.Web.Utility;

public sealed class EventPostRequestBodyStream : Stream, IEventPostBodyReadState
{
    public const long KestrelBodyLimitSlopBytes = 4096;

    private readonly Stream _inner;
    private readonly long _maximumBytes;
    private long _bytesRead;

    public EventPostRequestBodyStream(Stream inner, long maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);

        _inner = inner;
        _maximumBytes = maximumBytes;
    }

    public int? RejectedStatusCode { get; private set; }
    public string? RejectionReason { get; private set; }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        _inner.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return _inner.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);

        if (count == 0 || RejectedStatusCode.HasValue)
            return 0;

        int readLength = GetReadLength(count);
        if (readLength == 0)
            return 0;

        try
        {
            int bytesRead = _inner.Read(buffer, offset, readLength);
            return HandleReadResult(bytesRead);
        }
        catch (HttpBadHttpRequestException ex)
        {
            Reject(ex.StatusCode, ex.Message);
            return 0;
        }
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.Length == 0 || RejectedStatusCode.HasValue)
            return 0;

        int readLength = GetReadLength(buffer.Length);
        if (readLength == 0)
            return 0;

        try
        {
            int bytesRead = await _inner.ReadAsync(buffer[..readLength], cancellationToken);
            return HandleReadResult(bytesRead);
        }
        catch (HttpBadHttpRequestException ex)
        {
            Reject(ex.StatusCode, ex.Message);
            return 0;
        }
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    private int GetReadLength(int requestedLength)
    {
        long remaining = _maximumBytes - _bytesRead;
        if (remaining < 0)
        {
            Reject(StatusCodes.Status413RequestEntityTooLarge, "Request body too large.");
            return 0;
        }

        if (remaining == 0)
            return 1;

        if (remaining >= requestedLength)
            return requestedLength;

        return (int)remaining + 1;
    }

    private int HandleReadResult(int bytesRead)
    {
        if (bytesRead == 0)
            return 0;

        long totalBytesRead = _bytesRead + bytesRead;
        if (totalBytesRead > _maximumBytes)
        {
            Reject(StatusCodes.Status413RequestEntityTooLarge, "Request body too large.");
            return 0;
        }

        _bytesRead = totalBytesRead;
        return bytesRead;
    }

    private void Reject(int statusCode, string reason)
    {
        RejectedStatusCode ??= statusCode;
        RejectionReason ??= reason;
    }
}
