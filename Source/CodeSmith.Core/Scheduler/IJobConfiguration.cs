using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeSmith.Core.Scheduler
{
    public interface IJobConfiguration
    {
        /// <summary>
        /// Gets or sets the name of the job.
        /// </summary>
        /// <value>The name of the job.</value>
        string Name { get; set; }

        /// <summary>
        /// Gets or sets the description for the job.
        /// </summary>
        /// <value>The description for the job.</value>
        string Description { get; set; }

        /// <summary>
        /// Gets or sets the group for the job.
        /// </summary>
        /// <value>The group for the job.</value>
        string Group { get; set; }

        /// <summary>
        /// Gets or sets the timer interval.
        /// </summary>
        TimeSpan Interval { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether Interval is a time of day.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if Interval is time of day; otherwise, <c>false</c>.
        /// </value>
        bool IsTimeOfDay { get; set; }

        /// <summary>
        /// Gets or sets the assembly type that contains the job to run.
        /// </summary>
        string Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to keep alive the instance between job runs.
        /// </summary>
        /// <value><c>true</c> to keep alive instance; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Setting this to true, the default value, will keep the <see cref="IJob"/> instance alive between runs.
        /// </remarks>
        bool KeepAlive { get; set; }

        /// <summary>
        /// Gets or sets the name of the provider that is used to lock the job when running.
        /// </summary>
        /// <value>The type to use to lock the job.</value>
        string JobLockProvider { get; set; }

        /// <summary>
        /// Gets or sets the job history provider.
        /// </summary>
        /// <value>The job history provider.</value>
        string JobHistoryProvider { get; set; }

        /// <summary>
        /// Gets the arguments.
        /// </summary>
        /// <value>The arguments.</value>
        IDictionary<string, object> Arguments { get; }
    }
}
