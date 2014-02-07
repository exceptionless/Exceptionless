using System;
using System.Threading;

namespace CodeSmith.Core.Extensions
{
    public static class RetryUtil
    {
        public static void Retry(this Action action, int attempts = 3, int retryTimeout = 100)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            do
            {
                try
                {
                    action();
                    return;
                }
                catch
                {
                    if (attempts <= 0)
                        throw;
                    Thread.Sleep(retryTimeout);
                }
            } while (attempts-- > 0);
        }
    }
}
