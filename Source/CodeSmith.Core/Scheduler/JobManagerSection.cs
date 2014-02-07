using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;

namespace CodeSmith.Core.Scheduler
{
    public class JobManagerSection : ConfigurationSection
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JobManagerSection"/> class.
        /// </summary>
        public JobManagerSection()
        {
            JobProviderPoll = TimeSpan.Zero;
        }

        /// <summary>
        /// Gets or sets the poll interval for calling <see cref="JobProvider"/>.IsReloadRequired.
        /// </summary>
        /// <remarks>Set to <see cref="TimeSpan"/>.Zero to disable reload checking.</remarks>
        [ConfigurationProperty("jobProviderPoll", DefaultValue = "0:0:0")]
        public TimeSpan JobProviderPoll
        {
            get { return (TimeSpan)this["jobProviderPoll"]; }
            set { this["jobProviderPoll"] = value; }
        }

        /// <summary>
        /// Gets the providers to configure jobs.
        /// </summary>
        /// <value>The job configuration providers.</value>
        [ConfigurationProperty("jobProviders")]
        public ProviderSettingsCollection JobProviders
        {
            get
            {
                return this["jobProviders"] as ProviderSettingsCollection;
            }
        }

        /// <summary>
        /// Gets the job lock providers.
        /// </summary>
        /// <value>The job lock providers.</value>
        [ConfigurationProperty("jobLockProviders")]
        public ProviderSettingsCollection JobLockProviders
        {
            get
            {
                return this["jobLockProviders"] as ProviderSettingsCollection;
            }
        }

        /// <summary>
        /// The jobs to schedule.
        /// </summary>
        [ConfigurationProperty("jobs")]
        public JobElementCollection Jobs
        {
            get
            {
                return this["jobs"] as JobElementCollection;
            }
        }
    }
}
