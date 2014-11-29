using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CodeSmith.Core.Component;
using CodeSmith.Core.Helpers;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Queues;
using Nito.AsyncEx;
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
                await ResetQueue();

                await queue.EnqueueAsync(new SimpleWorkItem {
                    Data = "Hello"
                });
                Assert.Equal(1, await queue.GetQueueCountAsync());

                var workItem = await queue.DequeueAsync(TimeSpan.Zero);
                Assert.NotNull(workItem);
                Assert.Equal("Hello", workItem.Value.Data);
                Assert.Equal(1, queue.DequeuedCount);

                await workItem.CompleteAsync();
                Assert.Equal(1, queue.CompletedCount);
                Assert.Equal(0, await queue.GetQueueCountAsync());
            }
        }

        [Fact]
        public async Task WillWaitForItem() {
            using (var queue = GetQueue()) {
                if (queue == null)
                    return;
                await ResetQueue();

                TimeSpan timeToWait = TimeSpan.FromSeconds(1);
                var sw = new Stopwatch();
                sw.Start();
                var workItem = await queue.DequeueAsync(timeToWait);
                sw.Stop();
                Trace.WriteLine(sw.Elapsed);
                Assert.Null(workItem);
                Assert.True(sw.Elapsed > timeToWait);

                Task.Factory.StartNewDelayed(100, async () => await queue.EnqueueAsync(new SimpleWorkItem {
                    Data = "Hello"
                }));
                sw.Reset();
                sw.Start();
                workItem = await queue.DequeueAsync(timeToWait);
                sw.Stop();
                Trace.WriteLine(sw.Elapsed);
                Assert.NotNull(workItem);
            }
        }

        [Fact]
        public async Task CanUseQueueWorker() {
            using (var queue = GetQueue()) {
                if (queue == null)
                    return;
                await ResetQueue();

                var resetEvent = new AsyncAutoResetEvent();
                queue.StartWorking(async w => {
                    Assert.Equal("Hello", w.Value.Data);
                    await w.CompleteAsync();
                    resetEvent.Set();
                });
                await queue.EnqueueAsync(new SimpleWorkItem {
                    Data = "Hello"
                });

                Assert.Equal(1, await queue.GetQueueCountAsync());
                await resetEvent.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(1, queue.CompletedCount);
                Assert.Equal(0, await queue.GetQueueCountAsync());
                Assert.Equal(0, queue.WorkerErrorCount);
            }
        }

        [Fact]
        public async Task CanHandleErrorInWorker() {
            using (var queue = GetQueue(1, retryDelay: TimeSpan.Zero)) {
                if (queue == null)
                    return;
                await ResetQueue();

                queue.StartWorking(w => {
                    Debug.WriteLine("WorkAction");
                    Assert.Equal("Hello", w.Value.Data);
                    queue.StopWorking();
                    throw new ApplicationException();
                });
                await queue.EnqueueAsync(new SimpleWorkItem {
                    Data = "Hello"
                });

                var success = await TaskHelper.DelayUntil(() => queue.WorkerErrorCount > 0, TimeSpan.FromSeconds(5));
                Assert.True(success);
                Assert.Equal(0, queue.CompletedCount);
                Assert.Equal(1, queue.WorkerErrorCount);

                success = await TaskHelper.DelayUntil(() => queue.GetQueueCountAsync().Result > 0, TimeSpan.FromSeconds(5));
                Assert.True(success);
                Assert.Equal(1, await queue.GetQueueCountAsync());
            }
        }

        [Fact]
        public async Task WorkItemsWillTimeout() {
            using (var queue = GetQueue(workItemTimeout: TimeSpan.FromMilliseconds(50))) {
                if (queue == null)
                    return;
                await ResetQueue();

                await queue.EnqueueAsync(new SimpleWorkItem {
                    Data = "Hello"
                });
                var workItem = await queue.DequeueAsync(TimeSpan.Zero);
                Assert.NotNull(workItem);
                Assert.Equal("Hello", workItem.Value.Data);

                Assert.Equal(0, await queue.GetQueueCountAsync());
                // wait for the task to be auto abandoned
                var sw = new Stopwatch();
                sw.Start();
                workItem = await queue.DequeueAsync(TimeSpan.FromSeconds(5));
                sw.Stop();
                Trace.WriteLine(sw.Elapsed);
                Assert.NotNull(workItem);
                await workItem.CompleteAsync();
                Assert.Equal(0, await queue.GetQueueCountAsync());
            }
        }

        [Fact]
        public async Task WorkItemsWillGetMovedToDeadletter() {
            using (var queue = GetQueue(retryDelay: TimeSpan.Zero)) {
                if (queue == null)
                    return;
                await ResetQueue();

                await queue.EnqueueAsync(new SimpleWorkItem {
                    Data = "Hello"
                });
                var workItem = await queue.DequeueAsync(TimeSpan.Zero);
                Assert.Equal("Hello", workItem.Value.Data);
                Assert.Equal(1, queue.DequeuedCount);

                Assert.Equal(0, await queue.GetQueueCountAsync());
                await workItem.AbandonAsync();
                Assert.Equal(1, queue.AbandonedCount);

                // work item should be retried 1 time.
                workItem = await queue.DequeueAsync(TimeSpan.FromSeconds(5));
                Assert.NotNull(workItem);
                Assert.Equal("Hello", workItem.Value.Data);
                Assert.Equal(2, queue.DequeuedCount);

                await workItem.AbandonAsync();
                // work item should be moved to deadletter _queue after retries.
                Assert.Equal(1, await queue.GetDeadletterCountAsync());
                Assert.Equal(2, queue.AbandonedCount);
            }
        }

        [Fact]
        public async Task CanAutoCompleteWorker() {
            using (var queue = GetQueue()) {
                if (queue == null)
                    return;
                await ResetQueue();

                var resetEvent = new AsyncAutoResetEvent();
                queue.StartWorking(async w => {
                    Assert.Equal("Hello", w.Value.Data);
                    resetEvent.Set();
                }, true);
                await queue.EnqueueAsync(new SimpleWorkItem {
                    Data = "Hello"
                });

                Assert.Equal(1, queue.EnqueuedCount);
                await resetEvent.WaitAsync(TimeSpan.FromSeconds(5));
                await Task.Delay(1000);
                Assert.Equal(0, await queue.GetQueueCountAsync());
                Assert.Equal(1, queue.CompletedCount);
                Assert.Equal(0, queue.WorkerErrorCount);
            }
        }

        [Fact]
        public async Task CanHaveMultipleQueueInstances() {
            using (var queue = GetQueue(retries: 0, retryDelay: TimeSpan.Zero)) {
                if (queue == null)
                    return;
                await ResetQueue();

                const int workItemCount = 10;
                const int workerCount = 3;
                var latch = new AsyncCountdownEvent(workItemCount);
                var info = new WorkInfo();
                var workers = new List<IQueue<SimpleWorkItem>>();

                for (int i = 0; i < workerCount; i++) {
                    workers.Add(await Task.Factory.StartNew(() => {
                        var q = GetQueue(retries: 0, retryDelay: TimeSpan.Zero);
                        q.StartWorking(async w => await DoWork(w, latch, info));
                        return q;
                    }));
                }

                Parallel.For(0, workItemCount, async i => await queue.EnqueueAsync(new SimpleWorkItem {
                    Data = "Hello",
                    Id = i
                }));

                latch.Wait(CancellationTokenHelpers.Timeout(TimeSpan.FromSeconds(10)).Token);
                await Task.Delay(TimeSpan.FromSeconds(3));
                Debug.WriteLine("Completed: {0} Abandoned: {1} Error: {2}", info.CompletedCount, info.AbandonCount, info.ErrorCount);
                Debug.WriteLine("Count: {0} Work: {1} Dead: {2}", await queue.GetQueueCountAsync(), await queue.GetWorkingCountAsync(), await queue.GetDeadletterCountAsync());
                Assert.Equal(workItemCount, queue.CompletedCount + await queue.GetDeadletterCountAsync());
                Assert.Equal(info.ErrorCount, queue.WorkerErrorCount);
                Assert.Equal(info.AbandonCount + info.ErrorCount, queue.AbandonedCount);
                
                workers.ForEach(w => w.Dispose());
            }
        }

        protected async Task ResetQueue() {
            var queue = GetQueue();
            if (queue == null)
                return;

            await queue.ResetQueueAsync();
        }

        private async Task DoWork(QueueEntry<SimpleWorkItem> w, AsyncCountdownEvent latch, WorkInfo info) {
            Debug.WriteLine("DoWork: " + Thread.CurrentThread.ManagedThreadId);
            Assert.Equal("Hello", w.Value.Data);
            latch.Signal();

            // randomly complete, abandon or blowup.
            if (RandomHelper.GetBool()) {
                Debug.WriteLine("Completing: {0}", w.Value.Id);
                await w.CompleteAsync();
                info.IncrementCompletedCount();
            } else if (RandomHelper.GetBool()) {
                Debug.WriteLine("Abandoning: {0}", w.Value.Id);
                await w.AbandonAsync();
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
