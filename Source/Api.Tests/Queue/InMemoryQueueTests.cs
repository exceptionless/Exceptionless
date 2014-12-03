using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CodeSmith.Core.Component;
using CodeSmith.Core.Helpers;
using Exceptionless.Core.Queues;
using Xunit;

namespace Exceptionless.Api.Tests.Queue {
    public class InMemoryQueueTests {
        private IQueue<SimpleWorkItem> _queue;

        protected virtual IQueue<SimpleWorkItem> GetQueue(int retries = 1, TimeSpan? workItemTimeout = null, TimeSpan? retryDelay = null) {
            if (_queue == null)
                _queue = new InMemoryQueue<SimpleWorkItem>(retries, workItemTimeout, retryDelay);

            return _queue;
        }

        [Fact]
        public async Task CanQueueAndDequeueWorkItem() {
            using (var queue = GetQueue()) {
                if (queue == null)
                    return;
                ResetQueue();

                queue.Enqueue(new SimpleWorkItem {
                    Data = "Hello"
                });
                Assert.Equal(1, queue.GetQueueCount());

                var workItem = queue.Dequeue(TimeSpan.Zero);
                Assert.NotNull(workItem);
                Assert.Equal("Hello", workItem.Value.Data);
                Assert.Equal(1, queue.DequeuedCount);

                workItem.Complete();
                Assert.Equal(1, queue.CompletedCount);
                Assert.Equal(0, queue.GetQueueCount());
            }
        }

        [Fact]
        public async Task WillWaitForItem() {
            using (var queue = GetQueue()) {
                if (queue == null)
                    return;
                ResetQueue();

                TimeSpan timeToWait = TimeSpan.FromSeconds(1);
                var sw = new Stopwatch();
                sw.Start();
                var workItem = queue.Dequeue(timeToWait);
                sw.Stop();
                Trace.WriteLine(sw.Elapsed);
                Assert.Null(workItem);
                Assert.True(sw.Elapsed > timeToWait.Subtract(TimeSpan.FromMilliseconds(10)));

                Task.Factory.StartNewDelayed(100, () => queue.Enqueue(new SimpleWorkItem {
                    Data = "Hello"
                }));
                sw.Reset();
                sw.Start();
                workItem = queue.Dequeue(timeToWait);
                workItem.Complete();
                sw.Stop();
                Trace.WriteLine(sw.Elapsed);
                Assert.NotNull(workItem);
            }
        }

        [Fact]
        public void CanUseQueueWorker() {
            using (var queue = GetQueue()) {
                if (queue == null)
                    return;
                ResetQueue();

                var resetEvent = new AutoResetEvent(false);
                queue.StartWorking(w => {
                    Assert.Equal("Hello", w.Value.Data);
                    w.Complete();
                    resetEvent.Set();
                });
                queue.Enqueue(new SimpleWorkItem {
                    Data = "Hello"
                });

                Assert.Equal(1, queue.GetQueueCount());
                resetEvent.WaitOne(TimeSpan.FromSeconds(5));
                Assert.Equal(1, queue.CompletedCount);
                Assert.Equal(0, queue.GetQueueCount());
                Assert.Equal(0, queue.WorkerErrorCount);
            }
        }

        [Fact]
        public void CanHandleErrorInWorker() {
            using (var queue = GetQueue(1, retryDelay: TimeSpan.Zero)) {
                if (queue == null)
                    return;
                ResetQueue();

                queue.StartWorking(w => {
                    Debug.WriteLine("WorkAction");
                    Assert.Equal("Hello", w.Value.Data);
                    queue.StopWorking();
                    throw new ApplicationException();
                });
                queue.Enqueue(new SimpleWorkItem {
                    Data = "Hello"
                });

                var success = await TaskHelper.DelayUntil(() => queue.WorkerErrorCount > 0, TimeSpan.FromSeconds(5));
                Assert.True(success);
                Assert.Equal(0, queue.CompletedCount);
                Assert.Equal(1, queue.WorkerErrorCount);

                success = await TaskHelper.DelayUntil(() => queue.GetQueueCount() > 0, TimeSpan.FromSeconds(5));
                Assert.True(success);
                Assert.Equal(1, queue.GetQueueCount());
            }
        }

        [Fact]
        public void WorkItemsWillTimeout() {
            using (var queue = GetQueue(workItemTimeout: TimeSpan.FromMilliseconds(50))) {
                if (queue == null)
                    return;
                ResetQueue();

                queue.Enqueue(new SimpleWorkItem {
                    Data = "Hello"
                });
                var workItem = queue.Dequeue(TimeSpan.Zero);
                Assert.NotNull(workItem);
                Assert.Equal("Hello", workItem.Value.Data);

                Assert.Equal(0, queue.GetQueueCount());
                // wait for the task to be auto abandoned
                var sw = new Stopwatch();
                sw.Start();
                workItem = queue.Dequeue(TimeSpan.FromSeconds(5));
                sw.Stop();
                Trace.WriteLine(sw.Elapsed);
                Assert.NotNull(workItem);
                workItem.Complete();
                Assert.Equal(0, queue.GetQueueCount());
            }
        }

        [Fact]
        public void WorkItemsWillGetMovedToDeadletter() {
            using (var queue = GetQueue(retryDelay: TimeSpan.Zero)) {
                if (queue == null)
                    return;
                ResetQueue();

                queue.Enqueue(new SimpleWorkItem {
                    Data = "Hello"
                });
                var workItem = queue.Dequeue(TimeSpan.Zero);
                Assert.Equal("Hello", workItem.Value.Data);
                Assert.Equal(1, queue.DequeuedCount);

                Assert.Equal(0, queue.GetQueueCount());
                workItem.Abandon();
                Assert.Equal(1, queue.AbandonedCount);

                // work item should be retried 1 time.
                workItem = queue.Dequeue(TimeSpan.FromSeconds(5));
                Assert.NotNull(workItem);
                Assert.Equal("Hello", workItem.Value.Data);
                Assert.Equal(2, queue.DequeuedCount);

                workItem.Abandon();
                // work item should be moved to deadletter _queue after retries.
                Assert.Equal(1, queue.GetDeadletterCount());
                Assert.Equal(2, queue.AbandonedCount);
            }
        }

        [Fact]
        public void CanAutoCompleteWorker() {
            using (var queue = GetQueue()) {
                if (queue == null)
                    return;
                ResetQueue();

                var resetEvent = new AutoResetEvent(false);
                queue.StartWorking(w => {
                    Assert.Equal("Hello", w.Value.Data);
                    resetEvent.Set();
                }, true);
                queue.Enqueue(new SimpleWorkItem {
                    Data = "Hello"
                });

                Assert.Equal(1, queue.EnqueuedCount);
                resetEvent.WaitOne(TimeSpan.FromSeconds(5));
                Thread.Sleep(1000);
                Assert.Equal(0, queue.GetQueueCount());
                Assert.Equal(1, queue.CompletedCount);
                Assert.Equal(0, queue.WorkerErrorCount);
            }
        }

        [Fact]
        public void CanHaveMultipleQueueInstances() {
            using (var queue = GetQueue(retries: 0, retryDelay: TimeSpan.Zero)) {
                if (queue == null)
                    return;
                ResetQueue();

                const int workItemCount = 10;
                const int workerCount = 3;
                var latch = new CountdownEvent(workItemCount);
                var info = new WorkInfo();
                var workers = new List<IQueue<SimpleWorkItem>>();

                for (int i = 0; i < workerCount; i++) {
                    workers.Add(Task.Run(() => {
                        var q = GetQueue(retries: 0, retryDelay: TimeSpan.Zero);
                        q.StartWorking(w => DoWork(w, latch, info));
                    }));
                }

                Parallel.For(0, workItemCount, i => queue.Enqueue(new SimpleWorkItem {
                    Data = "Hello",
                    Id = i
                }));

                latch.Wait(TimeSpan.FromSeconds(10));
                Thread.Sleep(TimeSpan.FromSeconds(3));
                Debug.WriteLine("Completed: {0} Abandoned: {1} Error: {2}", info.CompletedCount, info.AbandonCount, info.ErrorCount);
                Debug.WriteLine("Count: {0} Work: {1} Dead: {2}", queue.GetQueueCount(), queue.GetWorkingCount(), queue.GetDeadletterCount());
                Assert.Equal(workItemCount, queue.CompletedCount + queue.GetDeadletterCount());
                Assert.Equal(info.ErrorCount, queue.WorkerErrorCount);
                Assert.Equal(info.AbandonCount + info.ErrorCount, queue.AbandonedCount);
                
                workers.ForEach(w => w.Dispose());
            }
        }

        protected void ResetQueue() {
            var queue = GetQueue();
            if (queue == null)
                return;

            queue.DeleteQueue();
        }

        private void DoWork(QueueEntry<SimpleWorkItem> w, CountdownEvent latch, WorkInfo info) {
            Debug.WriteLine("DoWork: " + Thread.CurrentThread.ManagedThreadId);
            Assert.Equal("Hello", w.Value.Data);
            latch.Signal();

            // randomly complete, abandon or blowup.
            if (RandomHelper.GetBool()) {
                Debug.WriteLine("Completing: {0}", w.Value.Id);
                w.Complete();
                info.IncrementCompletedCount();
            } else if (RandomHelper.GetBool()) {
                Debug.WriteLine("Abandoning: {0}", w.Value.Id);
                w.Abandon();
                info.IncrementAbandonCount();
            } else {
                Debug.WriteLine("Erroring: {0}", w.Value.Id);
                info.IncrementErrorCount();
                throw new ApplicationException();
            }
        }
    }

    public class WorkInfo {
        private int _abandonCount = 0;
        private int _errorCount = 0;
        private int _completedCount = 0;

        public int AbandonCount { get { return _abandonCount; } }
        public int ErrorCount { get { return _errorCount; } }
        public int CompletedCount { get { return _completedCount; } }

        public void IncrementAbandonCount() {
            Interlocked.Increment(ref _abandonCount);
        }

        public void IncrementErrorCount() {
            Interlocked.Increment(ref _errorCount);
        }

        public void IncrementCompletedCount() {
            Interlocked.Increment(ref _completedCount);
        }
    }
}
