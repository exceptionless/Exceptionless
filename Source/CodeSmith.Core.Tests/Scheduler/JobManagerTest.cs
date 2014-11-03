//using System;
//using CodeSmith.Core.Threading;
//using NUnit.Framework;
//using CodeSmith.Core.Scheduler;
//using System.Threading;

//namespace CodeSmith.Core.Tests.Scheduler {
//    [TestFixture]
//    public class JobManagerTest {
//        [Test]
//        public void Init() {
//            JobManager.Current.Initialize();

//            Assert.Greater(JobManager.Current.Jobs.Count, 0);
//        }

//        [Test, Ignore("This test requires a large amount of time to execute.")]
//        public void Start() {
//            JobManager.Current.Initialize();

//            Assert.Greater(JobManager.Current.Jobs.Count, 0);

//            Job j = JobManager.Current.Jobs[0];

//            JobManager.Current.Start();

//            while (j.LastStatus != JobStatus.Completed) {
//                Thread.Sleep(300);
//                if (j.LastStatus == JobStatus.Error)
//                    break;
//            }

//            Thread.Sleep(Convert.ToInt32(TimeSpan.FromMinutes(5).TotalMilliseconds));
//            Console.WriteLine("End");
//        }

//        [Test]
//        public void Reload() {
//            JobManager.Current.Start();
//            Assert.Greater(JobManager.Current.Jobs.Count, 0);

//            JobManager.Current.Reload();
//            Assert.Greater(JobManager.Current.Jobs.Count, 0);
//        }

//        [Test]
//        public void MultiThreadRun() {
//            var smartThreadPool = new SmartThreadPool(SmartThreadPool.DefaultIdleTimeout, 15, 10);

//            for (int i = 0; i < 10; i++) {
//                //run 10 concurrent processes
//                smartThreadPool.QueueWorkItem(new WorkItemCallback(Run), null);
//            }

//            Thread.Sleep(1000);
//            smartThreadPool.WaitForIdle(TimeSpan.FromMinutes(5));

//            Console.WriteLine("Jobs Done.");

//        }

//        public void MultiThreadRunCrazyThreads() {
//            var smartThreadPool = new SmartThreadPool(SmartThreadPool.DefaultIdleTimeout, 50, 10);

//            for (int i = 0; i < 10000; i++) {
//                //run 10000 concurrent processes
//                smartThreadPool.QueueWorkItem(new WorkItemCallback(RunLogging), null);
//            }

//            Thread.Sleep(1000);
//            smartThreadPool.WaitForIdle(TimeSpan.FromMinutes(1));

//            Console.WriteLine("Jobs Done.");

//        }

//        [Test, Ignore("This test requires a large amount of time to execute.")]
//        public void MultiThreadRunCrazyThreads2() {
//            var smartThreadPool = new SmartThreadPool(SmartThreadPool.DefaultIdleTimeout, 50, 10);

//            for (int i = 0; i < 100; i++) {
//                var thread = new Thread(RunCrazyThread);
//                thread.Start();
//            }

//            Thread.Sleep(1000);
//            smartThreadPool.WaitForIdle(TimeSpan.FromMinutes(1));

//            Console.WriteLine("Jobs Done.");
//        }

//        [Test, Ignore("This test requires a large amount of time to execute.")]
//        public void MultiThreadRunCrazyThreads3() {
//            var smartThreadPool = new SmartThreadPool(SmartThreadPool.DefaultIdleTimeout, 50, 10);
//            Console.WriteLine("MultiThreadRunCrazyThreads3() is running on Thread#: {0}", Thread.CurrentThread.ManagedThreadId);
//            for (int i = 0; i < 10; i++) {
//                var thread = new Thread(RunCrazyThread);
//                thread.Start();

//                for (int i2332 = 0; i2332 < 5; i2332++) {
//                    smartThreadPool.QueueWorkItem(new WorkItemCallback(RunLogging), null);
//                    JobManager.Current.Stop();
//                }


//                for (int i33 = 0; i33 < 5; i33++) {
//                    for (int i233 = 0; i233 < 5; i233++) {

//                        smartThreadPool.QueueWorkItem(new WorkItemCallback(RunLogging), null);
//                    }

//                    thread = new Thread(RunCrazyThread);
//                    thread.Start();
//                    JobManager.Current.Reload(true);
//                    Console.WriteLine("MultiThreadRunCrazyThreads3() is running on Thread#: {0}", Thread.CurrentThread.ManagedThreadId);
//                }

//                for (int i22 = 0; i22 < 5; i22++) {
//                    thread = new Thread(RunCrazyThread);
//                    thread.Start();
//                    for (int i2 = 0; i2 < 5; i2++) {
//                        thread = new Thread(RunCrazyThread);
//                        Console.WriteLine("MultiThreadRunCrazyThreads3() is running on Thread#: {0}", Thread.CurrentThread.ManagedThreadId);
//                        JobManager.Current.Reload(true);
//                        thread.Start();
//                        JobManager.Current.Stop();
//                    }
//                }

//                for (int i233 = 0; i233 < 5; i233++) {
//                    smartThreadPool.QueueWorkItem(new WorkItemCallback(RunLogging), null);
//                }

//                for (int i3 = 0; i3 < 5; i3++) {
//                    thread = new Thread(RunCrazyThread);
//                    thread.Start();
//                    Console.WriteLine("MultiThreadRunCrazyThreads3() is running on Thread#: {0}", Thread.CurrentThread.ManagedThreadId);
//                    JobManager.Current.Stop();
//                    JobManager.Current.Reload(true);
//                    JobManager.Current.Start();
//                }
//            }

//            Thread.Sleep(100);
//            smartThreadPool.WaitForIdle(TimeSpan.FromMinutes(1));

//            Console.WriteLine("Jobs Done.");
//        }

//        [Test, Ignore("This test requires a large amount of time to execute.")]
//        public void MultiThreadRunCrazyThreads4() {
//            var smartThreadPool = new SmartThreadPool(SmartThreadPool.DefaultIdleTimeout, 50, 10);

//            JobManager.Current.Start();

//            int i = 0;
//            while (i++ < 320) {
//                if (i % 20 == 0)
//                    smartThreadPool.QueueWorkItem(StopJobManager, null);
//                if (i % 37 == 0) {
//                    smartThreadPool.QueueWorkItem(StopJobManager, null);
//                    smartThreadPool.QueueWorkItem(StartJobManager, null);
//                    JobManager.Current.Start();
//                    smartThreadPool.QueueWorkItem(StopJobManager, null);
//                    JobManager.Current.Stop();
//                } else if (i % 30 == 0)
//                    smartThreadPool.QueueWorkItem(ReloadJobManager, null);
//                if (i % 125 == 0) {
//                    smartThreadPool.QueueWorkItem(StopJobManager, null);
//                    smartThreadPool.QueueWorkItem(StartJobManager, null);
//                    JobManager.Current.Reload();
//                    smartThreadPool.QueueWorkItem(StopJobManager, null);
//                    smartThreadPool.QueueWorkItem(ReloadJobManager, null);
//                    smartThreadPool.QueueWorkItem(StopJobManager, null);
//                    smartThreadPool.QueueWorkItem(StartJobManager, null);
//                }
//                if (i % 155 == 0) {
//                    smartThreadPool.QueueWorkItem(StopJobManager, null);
//                    smartThreadPool.QueueWorkItem(StartJobManager, null);
//                    smartThreadPool.QueueWorkItem(StartJobManager, null);
//                    smartThreadPool.QueueWorkItem(ReloadJobManager, null);
//                }

//                Thread.Sleep(500);
//            }

//            //for (int i = 0; i < 5; i++)
//            //{
//            //    var thread = new Thread(RunStartStopThread);
//            //    thread.Start();
//            //}

//            //RunCrazyThread();

//            Console.WriteLine("Jobs Done.");
//        }

//        [Test, Ignore("This test requires a large amount of time to execute.")]
//        public void MultiThreadRunCrazyThreads5() {
//            JobManager.Current.Start();

//            for (int i = 0; i < 500; i++) {
//                var thread = new Thread(RunCrazyStartStopThread);
//                var thread2 = new Thread(RunCrazyStartStopThread);
//                thread.Start();
//                thread2.Start();
//            }
//        }

//        [Test, Ignore("This test requires a large amount of time to execute.")]
//        public void MultiThreadRunCrazyThreads6() {
//            JobManager.Current.Start();

//            for (int i = 0; i < 500; i++) {
//                var thread = new Thread(RunStartStopThread);
//                var thread2 = new Thread(RunStartStopThread);
//                thread.Start();
//                thread2.Start();
//            }
//        }

//        private void RunCrazyThread() {
//            Console.WriteLine("RunCrazyThread() is running on Thread#: {0}", Thread.CurrentThread.ManagedThreadId);
//            var smartThreadPool = new SmartThreadPool(SmartThreadPool.DefaultIdleTimeout, 50, 10);
//            for (int i = 0; i < 5; i++) {
//                smartThreadPool.QueueWorkItem(new WorkItemCallback(RunLogging), null);

//                if (i % 2 == 0)
//                    smartThreadPool.QueueWorkItem(StopJobManager, null);
//                else if (i % 3 == 0)
//                    smartThreadPool.QueueWorkItem(ReloadJobManager, null);
//                else
//                    smartThreadPool.QueueWorkItem(StartJobManager, null);

//            }

//            smartThreadPool.WaitForIdle(TimeSpan.FromMinutes(1));
//        }

//        private void RunStartStopThread() {
//            var smartThreadPool = new SmartThreadPool(SmartThreadPool.DefaultIdleTimeout, 50, 10);
//            Console.WriteLine("RunStartStopThread() is running on Thread#: {0}", Thread.CurrentThread.ManagedThreadId);
//            for (int i = 0; i < 3; i++) {
//                if (i == 2)
//                    smartThreadPool.QueueWorkItem(StopJobManager, null);
//                else if (i == 3)
//                    smartThreadPool.QueueWorkItem(ReloadJobManager, null);
//                else
//                    smartThreadPool.QueueWorkItem(StartJobManager, null);

//                Thread.Sleep(1000);
//            }

//            smartThreadPool.WaitForIdle(TimeSpan.FromMinutes(1));
//        }

//        private void RunCrazyStartStopThread() {
//            var smartThreadPool = new SmartThreadPool(SmartThreadPool.DefaultIdleTimeout, 50, 10);
//            Console.WriteLine("RunCrazyStartStopThread() is running on Thread#: {0}", Thread.CurrentThread.ManagedThreadId);
//            for (int i = 0; i < 150; i++) {
//                RunCrazyThread();

//                if (i % 2 == 0)
//                    smartThreadPool.QueueWorkItem(StopJobManager, null);
//                else if (i % 3 == 0)
//                    smartThreadPool.QueueWorkItem(ReloadJobManager, null);
//                else
//                    smartThreadPool.QueueWorkItem(StartJobManager, null);

//                RunCrazyThread();
//            }

//            smartThreadPool.WaitForIdle(TimeSpan.FromMinutes(1));
//        }

//        private object StopJobManager(object state) {
//            Console.WriteLine("StopJobManager() is running on Thread#: {0}", Thread.CurrentThread.ManagedThreadId);
//            JobManager.Current.Stop();

//            return null;
//        }

//        private object StartJobManager(object state) {
//            Console.WriteLine("StartJobManager() is running on Thread#: {0}", Thread.CurrentThread.ManagedThreadId);
//            JobManager.Current.Start();

//            return null;
//        }

//        private object ReloadJobManager(object state) {
//            Console.WriteLine("ReloadJobManager() is running on Thread#: {0}", Thread.CurrentThread.ManagedThreadId);
//            JobManager.Current.Reload();

//            return null;
//        }

//        public object RunLogging(object state) {
//            Console.WriteLine("RunLogging(state) is running on Thread#: {0}", Thread.CurrentThread.ManagedThreadId);

//            var lockProvider = new StaticLockProvider();
//            var jobType = typeof(LoggingSleepJob);
//            var configuration = new JobConfiguration {
//                Description = "test job",
//                Name = "test",
//                Interval = TimeSpan.FromSeconds(1),
//            };

//            configuration.Arguments.Add("sleep", 30);
//            Job j = new Job(configuration, jobType, lockProvider, null, null);

//            j.Run();
//            return null;
//        }

//        public object RunLoggingLongWait(object state) {
//            Console.WriteLine("RunLoggingLongWait(state) is running on Thread#: {0}", Thread.CurrentThread.ManagedThreadId);

//            var lockProvider = new StaticLockProvider();
//            var jobType = typeof(LoggingSleepJob);
//            var configuration = new JobConfiguration {
//                Description = "test job",
//                Name = "test",
//                Interval = TimeSpan.FromSeconds(10),
//            };

//            configuration.Arguments.Add("sleep", 3600);
//            Job j = new Job(configuration, jobType, lockProvider, null, null);

//            j.Run();
//            return null;
//        }

//        public object Run(object state) {
//            var lockProvider = new StaticLockProvider();
//            var jobType = typeof(SleepJob);
//            var configuration = new JobConfiguration {
//                Description = "test job",
//                Name = "test",
//                Interval = TimeSpan.FromSeconds(1),
//            };

//            configuration.Arguments.Add("sleep", 30);
//            Job j = new Job(configuration, jobType, lockProvider, null, null);

//            j.Run();
//            return null;
//        }
//    }
//}