using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Threading.Tasks;

namespace Exceptionless.Extensions {
    internal static class EnumerableExtensions {
        public static void AddRange<T>(this ICollection<T> list, IEnumerable<T> range) {
            foreach (var r in range)
                list.Add(r);
        }

        public static Task ForEachAsync<T>(this IEnumerable<T> source, Func<T, Task> body) {
            return Task.Factory.WhenAll(source.Select(body));
        }
    }
}
