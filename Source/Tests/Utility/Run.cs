using System;
using System.Linq;
using System.Threading.Tasks;

namespace Exceptionless.Api.Tests.Utility {
    public static class Run {
        public static Task InParallelAsync(int iterations, Func<int, Task> work) {
            return Task.WhenAll(Enumerable.Range(1, iterations).Select(i => Task.Run(() => work(i))));
        }
    }
}