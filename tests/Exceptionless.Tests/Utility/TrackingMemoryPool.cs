using System.Buffers;

namespace Exceptionless.Tests.Utility;

internal sealed class TrackingMemoryPool : MemoryPool<byte>
{
    private int _outstandingRentals;

    public int OutstandingRentals => Volatile.Read(ref _outstandingRentals);

    public override int MaxBufferSize => Int32.MaxValue;

    public override IMemoryOwner<byte> Rent(int minBufferSize = -1)
    {
        Interlocked.Increment(ref _outstandingRentals);
        return new TrackingMemoryOwner(new byte[Math.Max(minBufferSize, 1)], this);
    }

    protected override void Dispose(bool disposing) { }

    private void Return() => Interlocked.Decrement(ref _outstandingRentals);

    private sealed class TrackingMemoryOwner(byte[] buffer, TrackingMemoryPool pool) : IMemoryOwner<byte>
    {
        private byte[]? _buffer = buffer;

        public Memory<byte> Memory => _buffer ?? throw new ObjectDisposedException(nameof(TrackingMemoryOwner));

        public void Dispose()
        {
            byte[]? returned = Interlocked.Exchange(ref _buffer, null);
            if (returned is null)
                return;

            returned.AsSpan().Clear();
            pool.Return();
        }
    }
}
