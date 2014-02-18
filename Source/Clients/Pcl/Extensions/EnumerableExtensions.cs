using System;
using System.Collections.Generic;

namespace Exceptionless.Extensions {
    internal static class EnumerableExtensions {
        public static void AddRange<T>(this ICollection<T> list, IEnumerable<T> range) {
            foreach (var r in range)
                list.Add(r);
        }
    }
}
