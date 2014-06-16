using System;
using System.Threading;
using System.Threading.Tasks;
using CodeSmith.Core.Helpers;
using CodeSmith.Core.Threading;
using Exceptionless.Core.Queues;
using Xunit;

namespace Exceptionless.Api.Tests.Queue {
    public class InMemoryQueueTests {
        [Fact]
        public void CanQueueAndDequeueWorkItem() {
            var queue = new InMemoryQueue<SimpleWorkItem>();
            queue.EnqueueAsync(new SimpleWorkItem {
                Data = "Hello"
            });
            Assert.Equal(1, queue.Count);

            var workItem = queue.DequeueAsync(0).Result;
            Assert.NotNull(workItem);
            Assert.Equal("Hello", workItem.Value.Data);
            Assert.Equal(0, queue.Count);
            Assert.Equal(1, queue.Dequeued);

            workItem.CompleteAsync().Wait();
            Assert.Equal(1, queue.Completed);
        }

        [Fact]
        public void CanUseQueueWorker() {
            var resetEvent = new AutoResetEvent(false);
            var queue = new InMemoryQueue<SimpleWorkItem>();
            queue.StartWorking(w => {
                Assert.Equal("Hello", w.Value.Data);
                w.CompleteAsync().Wait();
                resetEvent.Set();
            });
            queue.EnqueueAsync(new SimpleWorkItem {
                Data = "Hello"
            });

            Assert.Equal(1, queue.Count);
            bool success = resetEvent.WaitOne(250);
            Assert.Equal(0, queue.Count);
            Assert.Equal(1, queue.Completed);
            Assert.True(success, "Failed to receive message.");
            Assert.Equal(0, queue.WorkerErrors);
        }

        [Fact]
        public void WorkItemsWillTimeout() {
            var queue = new InMemoryQueue<SimpleWorkItem>(workItemTimeoutMilliseconds: 10);
            queue.EnqueueAsync(new SimpleWorkItem {
                Data = "Hello"
            });
            var workItem = queue.DequeueAsync(0).Result;
            Assert.Equal("Hello", workItem.Value.Data);

            Assert.Equal(0, queue.Count);
            // wait for the task to be auto abandoned
            Task.Delay(100).Wait();
            Assert.Equal(1, queue.Count);
            Assert.Equal(1, queue.Abandoned);
        }

        [Fact]
        public void WorkItemsWillGetMovedToDeadletter() {
            var queue = new InMemoryQueue<SimpleWorkItem>(retries: 1, workItemTimeoutMilliseconds: 10);
            queue.EnqueueAsync(new SimpleWorkItem {
                Data = "Hello"
            });
            var workItem = queue.DequeueAsync(0).Result;
            Assert.Equal("Hello", workItem.Value.Data);
            Assert.Equal(1, queue.Dequeued);

            Assert.Equal(0, queue.Count);
            // wait for the task to be auto abandoned
            Task.Delay(100).Wait();
            Assert.Equal(1, queue.Count);
            Assert.Equal(1, queue.Abandoned);

            // work item should be retried 1 time.
            workItem = queue.DequeueAsync(0).Result;
            Assert.Equal("Hello", workItem.Value.Data);
            Assert.Equal(2, queue.Dequeued);

            Task.Delay(100).Wait();
            // work item should be moved to deadletter queue after retries.
            Assert.Equal(1, queue.DeadletterCount);
            Assert.Equal(2, queue.Abandoned);
        }

        [Fact]
        public void CanAutoCompleteWorker() {
            var resetEvent = new AutoResetEvent(false);
            var queue = new InMemoryQueue<SimpleWorkItem>(workItemTimeoutMilliseconds: 100);
            queue.StartWorking(w => {
                Assert.Equal("Hello", w.Value.Data);
                resetEvent.Set();
            }, true);
            queue.EnqueueAsync(new SimpleWorkItem {
                Data = "Hello"
            });

            Assert.Equal(1, queue.Count);
            bool success = resetEvent.WaitOne(100);
            Assert.True(success, "Failed to receive message.");
            Task.Delay(25).Wait();
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
            var queue = new InMemoryQueue<SimpleWorkItem>(retries: 1, workItemTimeoutMilliseconds: 50);
            Task.Factory.StartNew(() => queue.StartWorking(w => DoWork(w, latch, ref abandonCount, ref errorCount)));
            Task.Factory.StartNew(() => queue.StartWorking(w => DoWork(w, latch, ref abandonCount, ref errorCount)));
            Task.Factory.StartNew(() => queue.StartWorking(w => DoWork(w, latch, ref abandonCount, ref errorCount)));

            Parallel.For(0, workItemCount, i => queue.EnqueueAsync(new SimpleWorkItem {
                Data = "Hello",
                Id = i
            }));

            bool success = latch.Wait(1000);
            Assert.True(success, "Failed to receive all work items.");
            Task.Delay(50).Wait();
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
