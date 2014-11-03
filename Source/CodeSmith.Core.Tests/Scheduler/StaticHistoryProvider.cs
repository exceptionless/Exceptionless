using System;
using CodeSmith.Core.Scheduler;

namespace CodeSmith.Core.Tests.Scheduler
{
    public class StaticHistoryProvider : JobHistoryProvider
    {
        public string LastResult { get; set; }
        public DateTime LastRunTime { get; set; }
        public JobStatus LastStatus { get; set; }

        public int RestoreCount { get; private set; }
        public int SaveCount { get; private set; }

        public override void RestoreHistory(JobRunner job)
        {
            RestoreCount++;

            job.LastResult = LastResult;
            job.LastRunStartTime = LastRunTime;
            job.LastStatus = LastStatus;
        }

        public override void SaveHistory(JobRunner job)
        {
            SaveCount++;

            LastResult = job.LastResult;
            LastRunTime = job.LastRunStartTime;
            LastStatus = job.LastStatus;
        }
    }
}
