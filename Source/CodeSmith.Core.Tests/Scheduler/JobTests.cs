//using System;
//using System.Text;
//using System.Threading;
//using CodeSmith.Core.Scheduler;
//using NUnit.Framework;

//namespace CodeSmith.Core.Tests.Scheduler {
//    [TestFixture]
//    public class JobTests {
//        [Test, Ignore("This test requires a large amount of time to execute.")]
//        public void StartMultiThreaded() {
//            Type jobType = typeof(SleepJob);
//            JobLockProvider jobLockProvider = new StaticLockProvider();
//            var jobConfiguration = new JobConfiguration {
//                Name = "SleepJob",
//                Description = "Test Sleep Job with xml history",
//                Interval = TimeSpan.FromSeconds(5),
//                Type = typeof(SleepJob).AssemblyQualifiedName,
//                JobLockProvider = typeof(StaticLockProvider).AssemblyQualifiedName,
//            };

//            jobConfiguration.Arguments.Add("sleep", 10);

//            var log = new StringBuilder();
//            var j = new Job(jobConfiguration, jobType, jobLockProvider, null, null);
//            JobManager.Current.JobStarting += (t, e) => log.AppendLine(e.ToString() + ":" + Thread.CurrentThread.ManagedThreadId);

//            Thread t1 = new Thread(j.Start);
//            Thread t2 = new Thread(j.Start);
//            Thread t3 = new Thread(j.Start);
//            Thread t4 = new Thread(j.Start);

//            t1.Start();
//            t2.Start();
//            Thread.Sleep(TimeSpan.FromSeconds(5));
//            t3.Start();
//            t4.Start();

//            // wait for job
//            Thread.Sleep(TimeSpan.FromSeconds(45));

//            string l = log.ToString();
//            Assert.IsNotNull(l);
//        }

//        [Test]
//        public void StartWithTimeOfDayLaterThenStart() {
//            Type jobType = typeof(SleepJob);
//            JobLockProvider jobLockProvider = new StaticLockProvider();
//            JobHistoryProvider jobHistoryProvider = new StaticHistoryProvider {
//                LastResult = string.Empty,
//                LastRunTime = DateTime.Now,
//                LastStatus = JobStatus.None
//            };

//            // run time 1 hour ago
//            DateTime runTime = DateTime.Now.Subtract(TimeSpan.FromHours(1));

//            var jobConfiguration = new JobConfiguration {
//                Name = "SleepJob",
//                Description = "Test Sleep Job with xml history",
//                Interval = runTime.TimeOfDay,
//                IsTimeOfDay = true,
//                Type = typeof(SleepJob).AssemblyQualifiedName,
//                JobLockProvider = typeof(StaticLockProvider).AssemblyQualifiedName,
//                JobHistoryProvider = typeof(StaticHistoryProvider).AssemblyQualifiedName
//            };

//            var j = new Job(jobConfiguration, jobType, jobLockProvider, jobHistoryProvider, null);

//            Assert.IsNotNull(j);
//            j.Start();

//            DateTime nextRun = runTime.AddDays(1);

//            Assert.AreEqual(nextRun.Date, j.NextRunTime.Date);
//            Assert.AreEqual(nextRun.Hour, j.NextRunTime.Hour);
//            Assert.AreEqual(nextRun.Minute, j.NextRunTime.Minute);
//        }

//        [Test, Ignore("This test requires a large amount of time to execute.")]
//        public void StartWithTimeOfDayOlderThenOneDay() {
//            Type jobType = typeof(SleepJob);
//            StaticLockProvider jobLockProvider = new StaticLockProvider();
//            StaticHistoryProvider jobHistoryProvider = new StaticHistoryProvider {
//                LastResult = string.Empty,
//                LastRunTime = DateTime.Now.Subtract(TimeSpan.FromDays(2)), // 2 days ago
//                LastStatus = JobStatus.None
//            };

//            // run time 1 hour ago
//            DateTime runTime = DateTime.Now.Subtract(TimeSpan.FromHours(1));

//            var jobConfiguration = new JobConfiguration {
//                Name = "SleepJob",
//                Description = "Test Sleep Job with xml history",
//                Interval = runTime.TimeOfDay,
//                IsTimeOfDay = true,
//                Type = typeof(SleepJob).AssemblyQualifiedName,
//                JobLockProvider = typeof(StaticLockProvider).AssemblyQualifiedName,
//                JobHistoryProvider = typeof(StaticHistoryProvider).AssemblyQualifiedName
//            };

//            var j = new Job(jobConfiguration, jobType, jobLockProvider, jobHistoryProvider, null);

//            Assert.IsNotNull(j);
//            Assert.AreEqual(1, jobHistoryProvider.RestoreCount);
//            Assert.AreEqual(JobStatus.None, j.LastStatus);

//            j.Start();

//            // wait 30 sec, then test
//            Thread.Sleep(TimeSpan.FromSeconds(30));

//            Assert.AreEqual(1, jobHistoryProvider.SaveCount);
//            Assert.AreEqual(JobStatus.Completed, j.LastStatus);

//            // should set next run time to tomorrow
//            DateTime nextRun = runTime.AddDays(1);

//            Assert.AreEqual(nextRun.Date, j.NextRunTime.Date);
//            Assert.AreEqual(nextRun.Hour, j.NextRunTime.Hour);
//            Assert.AreEqual(nextRun.Minute, j.NextRunTime.Minute);
//        }

//        [Test]
//        public void StartWithTimeOfDayLaterToday() {
//            Type jobType = typeof(SleepJob);
//            JobLockProvider jobLockProvider = new StaticLockProvider();
//            JobHistoryProvider jobHistoryProvider = new StaticHistoryProvider {
//                LastResult = string.Empty,
//                LastRunTime = DateTime.Now,
//                LastStatus = JobStatus.None
//            };

//            // run time 2 hours from now
//            DateTime runTime = DateTime.Now.AddHours(2);

//            var jobConfiguration = new JobConfiguration {
//                Name = "SleepJob",
//                Description = "Test Sleep Job with xml history",
//                Interval = runTime.TimeOfDay,
//                IsTimeOfDay = true,
//                Type = typeof(SleepJob).AssemblyQualifiedName,
//                JobLockProvider = typeof(StaticLockProvider).AssemblyQualifiedName,
//                JobHistoryProvider = typeof(StaticHistoryProvider).AssemblyQualifiedName
//            };

//            var j = new Job(jobConfiguration, jobType, jobLockProvider, jobHistoryProvider, null);

//            Assert.IsNotNull(j);
//            j.Start();

//            Assert.AreEqual(runTime.Date, j.NextRunTime.Date);
//            Assert.AreEqual(runTime.Hour, j.NextRunTime.Hour);
//            Assert.AreEqual(runTime.Minute, j.NextRunTime.Minute);
//        }
//    }
//}