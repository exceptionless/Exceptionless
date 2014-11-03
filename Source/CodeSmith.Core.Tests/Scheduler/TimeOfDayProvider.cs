using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodeSmith.Core.Scheduler;

namespace CodeSmith.Core.Tests.Scheduler
{
    public class TimeOfDayProvider : JobProvider
    {
        private readonly List<IJobConfiguration> _jobs;
        private DateTime _nextLoad;

        public TimeOfDayProvider()
        {
            _jobs = new List<IJobConfiguration>();
            _jobs.Add(new JobConfiguration()
                         {
                             Name = "SleepJob",
                             Description = "Test Sleep Job with xml history",
                             Interval = new TimeSpan(13, 0, 0),
                             IsTimeOfDay = true,
                             Type = typeof(SleepJob).AssemblyQualifiedName,
                             JobLockProvider = typeof(StaticLockProvider).AssemblyQualifiedName,
                             JobHistoryProvider = typeof(StaticHistoryProvider).AssemblyQualifiedName
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
