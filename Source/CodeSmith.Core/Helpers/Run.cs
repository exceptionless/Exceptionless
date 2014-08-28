using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Web.UI.WebControls;

namespace CodeSmith.Core.Helpers
{
    public static class Run
    {
        private static readonly ConcurrentDictionary<Delegate, object> _onceCalls = new ConcurrentDictionary<Delegate, object>(new LambdaComparer<Delegate>(CompareDelegates));
        public static void Once(Action action) {
            if (_onceCalls.TryAdd(action, null))
                action();
        }

        private static int CompareDelegates(Delegate del1, Delegate del2) {
            if (del1 == null)
                return -1;
            if (del2 == null)
                return 1;

            return GetDelegateHashCode(del1).CompareTo(GetDelegateHashCode(del2));
        }

        private static int GetDelegateHashCode(Delegate obj) {
            if (obj == null)
                return 0;

            return obj.Method.GetHashCode() ^ obj.GetType().GetHashCode();
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

        public static Task InBackground(Action action, int? maxFaults = null, TimeSpan? restartInterval = null) {
            return InBackground(t => action(), null, maxFaults, restartInterval);
        }

        public static Task InBackground(Action<CancellationToken> action, CancellationToken? token = null, int? maxFaults = null, TimeSpan? restartInterval = null) {
            if (!token.HasValue)
                token = CancellationToken.None;

            if (!maxFaults.HasValue)
                maxFaults = Int32.MaxValue;

            if (!restartInterval.HasValue)
                restartInterval = TimeSpan.FromMilliseconds(100);

            if (action == null)
                throw new ArgumentNullException("action");

            return Task.Factory.StartNew(() => {
                do {
                    try {
                        action(token.Value);
                    } catch {
                        if (maxFaults <= 0)
                            throw;

                        Task.Delay(restartInterval.Value, token.Value).Wait();
                    }
                } while (!token.Value.IsCancellationRequested && maxFaults-- > 0);
            }, token.Value, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }
}
