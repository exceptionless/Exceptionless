using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Utility {
    public static class Run {
        public static Task InParallel(int iterations, Func<int, Task> work) {
            return Task.WhenAll(Enumerable.Range(1, iterations).Select(i => Task.Run(() => work(i))));
        }

        public static Task Multiple(int iterations, Func<int, Task> work) {
            return Task.WhenAll(Enumerable.Range(1, iterations).Select(work));
        }

        public static Task WithRetriesAsync(Action action, int attempts = 3, TimeSpan? retryInterval = null) {
            return WithRetriesAsync<object>(() => {
                action();
                return null;
            }, attempts, retryInterval);
        }

        public static async Task<T> WithRetriesAsync<T>(Func<Task<T>> action, int attempts = 3, TimeSpan? retryInterval = null) {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            do {
                try {
                    return await action().AnyContext();
                } catch {
                    if (attempts <= 0)
                        throw;

                    if (retryInterval != null)
                        await Task.Delay(retryInterval.Value).AnyContext();
                    else
                        await SleepBackOffMultiplierAsync(attempts).AnyContext();
                }
            } while (attempts-- > 1);

            throw new ApplicationException("Should not get here.");
        }

        private static async Task SleepBackOffMultiplierAsync(int i) {
            var rand = new Random(Guid.NewGuid().GetHashCode());
            var nextTry = rand.Next((int)Math.Pow(i, 2), (int)Math.Pow(i + 1, 2) + 1);

            await Task.Delay(nextTry).AnyContext();
        }
    }
}