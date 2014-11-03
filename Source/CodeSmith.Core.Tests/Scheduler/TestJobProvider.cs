using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodeSmith.Core.Scheduler;

namespace CodeSmith.Core.Tests.Scheduler
{
    public class TestJobProvider : JobProvider
    {
        private readonly List<IJobConfiguration> _jobs;
        private DateTime _nextLoad;

        public TestJobProvider()
        {
            _jobs = new List<IJobConfiguration>();
            _jobs.Add(new JobConfiguration()
                         {
                             Name = "test",
                             Description = "test",
                             Interval = TimeSpan.FromSeconds(30),
                             Type = typeof(SleepJob).AssemblyQualifiedName,
                             JobLockProvider = typeof(StaticLockProvider).AssemblyQualifiedName
                         });
            _jobs.Add(new JobConfiguration()
            {
                Name = "logging test",
                Description = "test",
                Interval = TimeSpan.FromSeconds(30),
                Type = typeof(LoggingSleepJob).AssemblyQualifiedName,
                JobLockProvider = typeof(StaticLockProvider).AssemblyQualifiedName
            });
        }

        #region Overrides of JobProvider

        public override IEnumerable<IJobConfiguration> GetJobs()
        {
            _nextLoad = DateTime.Now.AddMinutes(1);
            return _jobs;
        }

        public override bool IsReloadRequired(DateTime lastLoad)
        {
            return _nextLoad < lastLoad;
        }
        #endregion
    }
}
