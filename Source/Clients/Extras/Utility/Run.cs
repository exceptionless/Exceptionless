using System;
using System.Threading;

namespace Exceptionless.Utility {
    public static class Run {
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
                retryInterval = TimeSpan.FromMilliseconds(new Random().Next(75, 125));

            do {
                try {
                    return action();
                } catch {
                    if (attempts <= 0)
                        throw;

                    Thread.Sleep(retryInterval.Value);
                }
            } while (attempts-- >= 1);

            throw new ApplicationException("Should not get here.");
        }
    }
}
