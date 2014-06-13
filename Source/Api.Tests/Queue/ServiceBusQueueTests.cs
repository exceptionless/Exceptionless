using System;
using System.Threading;
using System.Threading.Tasks;
using CodeSmith.Core.Helpers;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core;
using Exceptionless.Core.Queues;
using Microsoft.ServiceBus;
using Xunit;

namespace Exceptionless.Api.Tests.Queue {
    public class ServiceBusQueueTests {
        private readonly ServiceBusQueue<SimpleWorkItem> _queue;
        
        public ServiceBusQueueTests() {
          if (!Settings.Current.UseAzureServiceBus)
                return;

            _queue = new ServiceBusQueue<SimpleWorkItem>(Settings.Current.AzureServiceBusConnectionString, 
                "test-_queue", 
                workItemTimeoutMilliseconds: 1000, 
                retries: 1, 
                shouldRecreate: true, 
                retryPolicy: new RetryExponential(TimeSpan.Zero, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(100), 1));    
        }

        [Fact]
        public void CanQueueAndDequeueWorkItem() {
            if (_queue == null)
                return;

            _queue.EnqueueAsync(new SimpleWorkItem {
                Data = "Hello"
            }).Wait();
            Assert.Equal(1, _queue.Count);

            var workItem = _queue.DequeueAsync(0).Result;
            Assert.NotNull(workItem);
            Assert.Equal("Hello", workItem.Value.Data);
            Assert.Equal(1, _queue.Dequeued);

            workItem.CompleteAsync().Wait();
            Assert.Equal(1, _queue.Completed);
            Assert.Equal(0, _queue.Count);
        }

        [Fact]
        public void CanUseQueueWorker() {
            if (_queue == null)
                return;

            var resetEvent = new AutoResetEvent(false);
            
            _queue.StartWorking(w => {
                Assert.Equal("Hello", w.Value.Data);
                w.CompleteAsync().Wait();
                resetEvent.Set();
            });
            _queue.EnqueueAsync(new SimpleWorkItem {
                Data = "Hello"
            }).Wait();

            Assert.Equal(1, _queue.Count);
            bool success = resetEvent.WaitOne(1000);
            Assert.Equal(1, _queue.Completed);
            Assert.Equal(0, _queue.Count);
            Assert.True(success, "Failed to receive message.");
            Assert.Equal(0, _queue.WorkerErrors);
        }

        [Fact]
        public void WorkItemsWillTimeout() {
            if (_queue == null)
                return;

            _queue.EnqueueAsync(new SimpleWorkItem {
                Data = "Hello"
            });
            var workItem = _queue.DequeueAsync(0).Result;
            Assert.Equal("Hello", workItem.Value.Data);

            Assert.Equal(0, _queue.Count);
            // wait for the task to be auto abandoned
            workItem = _queue.DequeueAsync(15000).Result;
            workItem.CompleteAsync().Wait();
            Assert.NotNull(workItem);
            Assert.Equal(0, _queue.Count);
        }

        [Fact]
        public void WorkItemsWillGetMovedToDeadletter() {
            if (_queue == null)
                return;

            _queue.EnqueueAsync(new SimpleWorkItem {
                Data = "Hello"
            });
            var workItem = _queue.DequeueAsync(0).Result;
            Assert.Equal("Hello", workItem.Value.Data);
            Assert.Equal(1, _queue.Dequeued);

            Assert.Equal(0, _queue.Count);
            workItem.AbandonAsync().Wait();
            Assert.Equal(1, _queue.Abandoned);

            // work item should be retried 1 time.
            workItem = _queue.DequeueAsync(15000).Result;
            Assert.Equal("Hello", workItem.Value.Data);
            Assert.Equal(2, _queue.Dequeued);

            workItem.AbandonAsync().Wait();
            // work item should be moved to deadletter _queue after retries.
            Assert.Equal(1, _queue.DeadletterCount);
            Assert.Equal(2, _queue.Abandoned);
        }

        [Fact]
        public void CanAutoCompleteWorker() {
            if (_queue == null)
                return;

            var resetEvent = new AutoResetEvent(false);
            _queue.StartWorking(w => {
                Assert.Equal("Hello", w.Value.Data);
                resetEvent.Set();
            }, true);
            _queue.EnqueueAsync(new SimpleWorkItem {
                Data = "Hello"
            });

            Assert.Equal(1, _queue.Enqueued);
            bool success = resetEvent.WaitOne(1000);
            Assert.True(success, "Failed to receive message.");
            Assert.Equal(0, _queue.Count);
            Assert.Equal(1, _queue.Completed);
            Assert.Equal(0, _queue.WorkerErrors);
        }

        [Fact]
        public void CanHaveMultipleWorkers() {
            if (_queue == null)
                return;

            const int workItemCount = 50;
            var latch = new CountDownLatch(workItemCount);
            int errorCount = 0;
            int abandonCount = 0;
            Task.Factory.StartNew(() => _queue.StartWorking(w => DoWork(w, latch, ref abandonCount, ref errorCount)));
            Task.Factory.StartNew(() => _queue.StartWorking(w => DoWork(w, latch, ref abandonCount, ref errorCount)));
            Task.Factory.StartNew(() => _queue.StartWorking(w => DoWork(w, latch, ref abandonCount, ref errorCount)));

            Parallel.For(0, workItemCount, i => _queue.EnqueueAsync(new SimpleWorkItem {
                Data = "Hello",
                Id = i
            }));

            Assert.Equal(workItemCount, _queue.Enqueued);
            bool success = latch.Wait(60000);
            Task.Delay(5000).Wait();
            Assert.True(success, "Failed to receive all work items.");
            Assert.Equal(workItemCount, _queue.Completed + _queue.DeadletterCount);
            Assert.Equal(errorCount, _queue.WorkerErrors);
            Assert.Equal(abandonCount + errorCount, _queue.Abandoned);
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