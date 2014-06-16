using System;
using System.Diagnostics;
using Exceptionless.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using Xunit;

namespace Client.Tests.Utility {
    public class AsyncTests {
        [Fact]
        public void CanChainForeach() {
            var task = Task.Factory.FromResult(0).Then(t => {
                foreach (var i in new[] { 1, 2, 3, 4, 5 }) {
                    int i1 = i;
                    t = t.Then(t1 => SomeAsync(i1));
                }

                return t;
            });
            task.Wait();
        }

        [Fact]
        public void Blah() {
            var task =  Task.Factory.FromResult(0).Then(t => {
                foreach (var i in new[] { 1, 2, 3, 4, 5 }) {
                    int i1 = i;
                    t = t.Then(t1 => SomeAsync(i1));
                }
            });
            task.Wait();
        }

        private Task SomeAsync(int i) {
            Debug.WriteLine(i);
            return Task.FromResult(0);
        }
    }
}
