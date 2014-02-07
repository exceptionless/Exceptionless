using System;
using System.Collections.Generic;
using CodeSmith.Core.Dependency;

namespace CodeSmith.Core.Scheduler
{
    public class JobContext : MarshalByRefObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.MarshalByRefObject" /> class. 
        /// </summary>
        public JobContext(string name, string description, DateTime lastRunTime, JobStatus lastStatus, IDictionary<string, object> arguments, Action<string> updateStatus, IDependencyResolver resolver)
        {
            UpdateStatusAction = updateStatus;
            Name = name;
            Description = description;
            LastRunTime = lastRunTime;
            LastStatus = lastStatus;
            Arguments = arguments;
        }

        /// <summary>
        /// Gets the name of the job.
        /// </summary>
        /// <value>The name of the job.</value>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the description for the job.
        /// </summary>
        /// <value>The description for the job.</value>
        public string Description { get; private set; }

        /// <summary>
        /// Gets the arguments.
        /// </summary>
        /// <value>The arguments.</value>
        public IDictionary<string, object> Arguments { get; private set; }

        /// <summary>
        /// Gets the last status.
        /// </summary>
        /// <value>The last status.</value>
        public JobStatus LastStatus { get; private set; }

        /// <summary>
        /// Gets the last run time.
        /// </summary>
        /// <value>The last run time.</value>
        public DateTime LastRunTime { get; private set; }

        public Action<string> UpdateStatusAction { get; private set; }

        public IDependencyResolver DependencyResolver { get; private set; }

        /// <summary>
        /// Updates the status.
        /// </summary>
        /// <param name="message">The message.</param>
        public void UpdateStatus(string message)
        {
            if (UpdateStatusAction != null)
                UpdateStatusAction(message);
        }
    }
}