using System;
using System.Collections.Concurrent;
using System.Threading;

namespace CodeSmith.Core.Helpers
{
    public static class Run
    {
        private static readonly ConcurrentDictionary<int, object> _onceCalls = new ConcurrentDictionary<int, object>();
        public static void Once(Action action) {
            if (_onceCalls.TryAdd(action.GetHashCode(), null))
                action();
        }

        public static void WithRetries(Action action, int attempts = 3, TimeSpan? retryInterval = null) {
            WithRetries<object>(() => {
                action();
                return null;
            }, attempts, retryInterval);
        }

        public static T WithRetries<T>(Func<T> action, int attempts = 3, TimeSpan? retryInterval = null) {
            if (action == null)
                throw new ArgumentNullException("action");

            if (!retryInterval.HasValue)
                retryInterval = TimeSpan.FromMilliseconds(100);

            do {
                try {
                    return action();
                } catch {
                    if (attempts <= 0)
                        throw;
                    Thread.Sleep(retryInterval.Value);
                }
            } while (attempts-- > 1);

            throw new ApplicationException("Should not get here.");
        }
    }
}
