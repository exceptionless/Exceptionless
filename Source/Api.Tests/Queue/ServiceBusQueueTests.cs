using System;
using System.Threading;
using System.Threading.Tasks;
using CodeSmith.Core.Helpers;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Queues;
using Microsoft.ServiceBus;
using Xunit;

namespace Exceptionless.Api.Tests.Queue {
    public class ServiceBusQueueTests {
        private const string CONNECTION_STRING = "<ConnectionStringHere>";
        private static readonly Lazy<ServiceBusQueue<SimpleWorkItem>> _queue = new Lazy<ServiceBusQueue<SimpleWorkItem>>(() =>
            new ServiceBusQueue<SimpleWorkItem>(CONNECTION_STRING, "test-queue",
                workItemTimeoutMilliseconds: 1000,
                retries: 1,
                shouldRecreate: true,
                retryPolicy: new RetryExponential(TimeSpan.Zero, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(100), 1)));

        [Fact]
        public void CanQueueAndDequeueWorkItem() {
            var queue = _queue.Value;
            queue.EnqueueAsync(new SimpleWorkItem {
                Data = "Hello"
            }).Wait();
            Assert.Equal(1, queue.Count);

            var workItem = queue.DequeueAsync(0).Result;
            Assert.NotNull(workItem);
            Assert.Equal("Hello", workItem.Value.Data);
            Assert.Equal(1, queue.Dequeued);

            workItem.CompleteAsync().Wait();
            Assert.Equal(1, queue.Completed);
            Assert.Equal(0, queue.Count);
        }

        [Fact]
        public void CanUseQueueWorker() {
            var resetEvent = new AutoResetEvent(false);
            var queue = _queue.Value;
            queue.StartWorking(w => {
                Assert.Equal("Hello", w.Value.Data);
                w.CompleteAsync().Wait();
                resetEvent.Set();
            });
            queue.EnqueueAsync(new SimpleWorkItem {
                Data = "Hello"
            }).Wait();

            Assert.Equal(1, queue.Count);
            bool success = resetEvent.WaitOne(1000);
            Assert.Equal(1, queue.Completed);
            Assert.Equal(0, queue.Count);
            Assert.True(success, "Failed to recieve message.");
            Assert.Equal(0, queue.WorkerErrors);
        }

        [Fact]
        public void WorkItemsWillTimeout() {
            var queue = _queue.Value;
            queue.EnqueueAsync(new SimpleWorkItem {
                Data = "Hello"
            });
            var workItem = queue.DequeueAsync(0).Result;
            Assert.Equal("Hello", workItem.Value.Data);

            Assert.Equal(0, queue.Count);
            // wait for the task to be auto abandoned
            workItem = queue.DequeueAsync(15000).Result;
            workItem.CompleteAsync().Wait();
            Assert.NotNull(workItem);
            Assert.Equal(0, queue.Count);
        }

        [Fact]
        public void WorkItemsWillGetMovedToDeadletter() {
            var queue = _queue.Value;
            queue.EnqueueAsync(new SimpleWorkItem {
                Data = "Hello"
            });
            var workItem = queue.DequeueAsync(0).Result;
            Assert.Equal("Hello", workItem.Value.Data);
            Assert.Equal(1, queue.Dequeued);

            Assert.Equal(0, queue.Count);
            workItem.AbandonAsync().Wait();
            Assert.Equal(1, queue.Abandoned);

            // work item should be retried 1 time.
            workItem = queue.DequeueAsync(15000).Result;
            Assert.Equal("Hello", workItem.Value.Data);
            Assert.Equal(2, queue.Dequeued);

            workItem.AbandonAsync().Wait();
            // work item should be moved to deadletter queue after retries.
            Assert.Equal(1, queue.DeadletterCount);
            Assert.Equal(2, queue.Abandoned);
        }

        [Fact]
        public void CanAutoCompleteWorker() {
            var resetEvent = new AutoResetEvent(false);
            var queue = _queue.Value;
            queue.StartWorking(w => {
                Assert.Equal("Hello", w.Value.Data);
                resetEvent.Set();
            }, true);
            queue.EnqueueAsync(new SimpleWorkItem {
                Data = "Hello"
            });

            Assert.Equal(1, queue.Enqueued);
            bool success = resetEvent.WaitOne(1000);
            Assert.True(success, "Failed to recieve message.");
            Assert.Equal(0, queue.Count);
            Assert.Equal(1, queue.Completed);
            Assert.Equal(0, queue.WorkerErrors);
        }

        [Fact]
        public void CanHaveMultipleWorkers() {
            const int workItemCount = 50;
            var latch = new CountDownLatch(workItemCount);
            int errorCount = 0;
            int abandonCount = 0;
            var queue = _queue.Value;
            Task.Factory.StartNew(() => queue.StartWorking(w => DoWork(w, latch, ref abandonCount, ref errorCount)));
            Task.Factory.StartNew(() => queue.StartWorking(w => DoWork(w, latch, ref abandonCount, ref errorCount)));
            Task.Factory.StartNew(() => queue.StartWorking(w => DoWork(w, latch, ref abandonCount, ref errorCount)));

            Parallel.For(0, workItemCount, i => queue.EnqueueAsync(new SimpleWorkItem {
                Data = "Hello",
                Id = i
            }));

            Assert.Equal(workItemCount, queue.Enqueued);
            bool success = latch.Wait(60000);
            Task.Delay(5000).Wait();
            Assert.True(success, "Failed to recieve all work items.");
            Assert.Equal(workItemCount, queue.Completed + queue.DeadletterCount);
            Assert.Equal(errorCount, queue.WorkerErrors);
            Assert.Equal(abandonCount + errorCount, queue.Abandoned);
        }

        private void DoWork(QueueEntry<SimpleWorkItem> w, CountDownLatch latch, ref int abandonCount, ref int errorCount) {
            Assert.Equal("Hello", w.Value.Data);
            latch.Signal();

            // randomly complete, abandon or blowup.
            if (RandomHelper.GetBool()) {
                Console.WriteLine("Completing: {0}", w.Value.Id);
                w.CompleteAsync().Wait();
            } else if (RandomHelper.GetBool()) {
                Console.WriteLine("Abandoning: {0}", w.Value.Id);
                w.AbandonAsync();
                Interlocked.Increment(ref abandonCount);
            } else {
                Console.WriteLine("Erroring: {0}", w.Value.Id);
                Interlocked.Increment(ref errorCount);
                throw new ApplicationException();
            }
        }
    }
}
