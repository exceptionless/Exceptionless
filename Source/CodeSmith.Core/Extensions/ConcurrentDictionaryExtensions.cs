using System;

#if PFX_LEGACY_3_5
using CodeSmith.Core.Collections;
using CodeSmith.Core.Threading;
#else
using System.Collections.Concurrent;
#endif

namespace CodeSmith.Core.Extensions
{
    /// <summary>
    /// http://msdn.microsoft.com/en-us/library/dd997369.aspx
    /// http://kozmic.pl/2010/08/06/concurrentdictionary-in-net-4-not-what-you-would-expect/
    /// http://codereview.stackexchange.com/questions/2025/extension-methods-to-make-concurrentdictionary-getoradd-and-addorupdate-thread-sa
    /// </summary>
    public static class ConcurrentDictionaryExtensions
    {
        public static K GetOrAddSafe<T, K>(this ConcurrentDictionary<T, Lazy<K>> dictionary, T key, Func<T, K> valueFactory)
        {
            Lazy<K> lazy = dictionary.GetOrAdd(key, new Lazy<K>(() => valueFactory(key)));
            return lazy.Value;
        }

        public static K AddOrUpdateSafe<T, K>(this ConcurrentDictionary<T, Lazy<K>> dictionary, T key, Func<T, K> addValueFactory, Func<T, K, K> updateValueFactory)
        {
            Lazy<K> lazy = dictionary.AddOrUpdate(key,
                new Lazy<K>(() => addValueFactory(key)), (k, oldValue) => new Lazy<K>(() => updateValueFactory(k, oldValue.Value)));
            return lazy.Value;
        }
    }
}
