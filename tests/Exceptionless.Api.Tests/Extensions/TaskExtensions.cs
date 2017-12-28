using System;
using System.Threading.Tasks;
using Foundatio.AsyncEx;
using Foundatio.Utility;

namespace Exceptionless.Api.Tests.Extensions {
    public static class TaskExtensions {
        public static Task WaitAsync(this AsyncCountdownEvent countdownEvent, TimeSpan timeout) {
            return Task.WhenAny(countdownEvent.WaitAsync(), SystemClock.SleepAsync(timeout));
        }
    }
}