using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeSmith.Core.Scheduler
{
    /// <summary>
    /// A class holding job configuration.
    /// </summary>
    public class JobConfiguration : IJobConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JobConfiguration" /> class.
        /// </summary>
        public JobConfiguration()
        {
            Arguments = new Dictionary<string, object>();
            Interval = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Gets or sets the name of the job.
        /// </summary>
        /// <value>The name of the job.</value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description for the job.
        /// </summary>
        /// <value>The description for the job.</value>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the group for the job.
        /// </summary>
        /// <value>The group for the job.</value>
        public string Group { get; set; }

        /// <summary>
        /// Gets or sets the timer interval.
        /// </summary>
        public TimeSpan Interval { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether Interval is a time of day.
        /// </summary>
        /// <value><c>true</c> if Interval is time of day; otherwise, <c>false</c>.</value>
        public bool IsTimeOfDay { get; set; }

        /// <summary>
        /// Gets or sets the assembly type that contains the job to run.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to keep alive the instance between job runs.
        /// </summary>
        /// <value><c>true</c> to keep alive instance; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Setting this to true, the default value, will keep the <see cref="IJob"/> instance alive between runs.
        /// </remarks>
        public bool KeepAlive { get; set; }

        /// <summary>
        /// Gets or sets the name of the provider that is used to lock the job when running.
        /// </summary>
        /// <value>The type to use to lock the job.</value>
        public string JobLockProvider { get; set; }

        /// <summary>
        /// Gets or sets the job history provider.
        /// </summary>
        /// <value>The job history provider.</value>
        public string JobHistoryProvider { get; set; }

        /// <summary>
        /// Gets or sets the job factory.
        /// </summary>
        /// <value>The job factory.</value>
        public string JobFactory { get; set; }

        /// <summary>
        /// Gets the arguments.
        /// </summary>
        /// <value>The arguments.</value>
        public IDictionary<string, object> Arguments { get; private set; }
    }
}
