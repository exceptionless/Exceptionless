using System.Diagnostics;
using System.Text;

namespace OpenTelemetry.Internal;

// Based on Microsoft.Extensions.ObjectPool
// https://github.com/dotnet/aspnetcore/blob/main/src/ObjectPool/src/DefaultObjectPool.cs
internal class StringBuilderPool
{
    internal static StringBuilderPool Instance = new();

    private protected readonly ObjectWrapper[] items;
    private protected StringBuilder? firstItem;

    public StringBuilderPool()
        : this(Environment.ProcessorCount * 2)
    {
    }

    public StringBuilderPool(int maximumRetained)
    {
        // -1 due to _firstItem
        items = new ObjectWrapper[maximumRetained - 1];
    }

    public int MaximumRetainedCapacity { get; set; } = 4 * 1024;

    public int InitialCapacity { get; set; } = 100;

    public StringBuilder Get()
    {
        var item = firstItem;
        if (item is null || Interlocked.CompareExchange(ref firstItem, null, item) != item)
        {
            var items = this.items;
            for (int i = 0; i < items.Length; i++)
            {
                item = items[i].Element;
                if (item is not null && Interlocked.CompareExchange(ref items[i].Element!, null, item) == item)
                {
                    return item;
                }
            }

            item = new StringBuilder(InitialCapacity);
        }

        return item;
    }

    public bool Return(StringBuilder item)
    {
        if (item.Capacity > MaximumRetainedCapacity)
        {
            // Too big. Discard this one.
            return false;
        }

        item.Clear();

        if (firstItem is not null || Interlocked.CompareExchange(ref firstItem, item, null) is not null)
        {
            var items = this.items;
            for (int i = 0; i < items.Length && Interlocked.CompareExchange(ref items[i].Element, item, null) is not null; ++i)
            {
            }
        }

        return true;
    }

    [DebuggerDisplay("{Element}")]
    private protected struct ObjectWrapper
    {
        public StringBuilder Element;
    }
}
