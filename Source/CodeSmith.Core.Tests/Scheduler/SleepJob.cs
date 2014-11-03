using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CodeSmith.Core.Scheduler;
using System.Threading;

namespace CodeSmith.Core.Tests.Scheduler
{
    public class SleepJob : Job
    {
        protected async override Task<JobResult> RunInternalAsync() {
            int sleep = 5;
            if (Context.Properties.ContainsKey("sleep"))
                sleep = (int)Context.Properties["sleep"];

            TimeSpan timeSpan = TimeSpan.FromSeconds(sleep);
            string message = string.Format("Sleep for {0} sec start.", timeSpan.TotalSeconds);

            Context.UpdateStatus(message);
            Debug.WriteLine(message);

            Thread.Sleep(sleep);

            return JobResult.SuccessWithMessage(String.Format("Sleep for {0} sec Complete.", timeSpan.TotalSeconds));
        }
    }
}
