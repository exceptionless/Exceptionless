using System;
using System.Collections.Generic;
using System.Configuration;

namespace CodeSmith.Core.Scheduler
{
    /// <summary>
    /// The job configuration element
    /// </summary>
    public class JobElement : ConfigurationElement, IJobConfiguration
    {
        private readonly IDictionary<string, object> _arguments = new Dictionary<string, object>();

        /// <summary>
        /// Initializes a new instance of the <see cref="JobElement"/> class.
        /// </summary>
        public JobElement()
        {
            Interval = TimeSpan.FromSeconds(30);
            KeepAlive = true;
            IsTimeOfDay = false;
            Description = String.Empty;
        }

        /// <summary>
        /// Gets or sets the name of the job.
        /// </summary>
        /// <value>The name of the job.</value>
        [ConfigurationProperty("name", IsKey = true, IsRequired = true)]
        public string Name
        {
            get { return (string) this["name"]; }
            set { this["name"] = value; }
        }

        /// <summary>
        /// Gets or sets the description for the job.
        /// </summary>
        /// <value>The description for the job.</value>
        [ConfigurationProperty("description", DefaultValue = "")]
        public string Description
        {
            get { return (string) this["description"]; }
            set { this["description"] = value; }
        }

        /// <summary>
        /// Gets or sets the group for the job.
        /// </summary>
        /// <value>The group for the job.</value>
        [ConfigurationProperty("group", DefaultValue = "")]
        public string Group
        {
            get { return (string)this["group"]; }
            set { this["group"] = value; }
        }

        /// <summary>
        /// Gets or sets the timer interval.
        /// </summary>
        [ConfigurationProperty("interval", DefaultValue = "0:0:30")]
        public TimeSpan Interval
        {
            get { return (TimeSpan) this["interval"]; }
            set { this["interval"] = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether Interval is a time of day.
        /// </summary>
        /// <value><c>true</c> if Interval is time of day; otherwise, <c>false</c>.</value>
        [ConfigurationProperty("isTimeOfDay", DefaultValue = false)]
        public bool IsTimeOfDay
        {
            get { return (bool)this["isTimeOfDay"]; }
            set { this["isTimeOfDay"] = value; }
        }

        /// <summary>
        /// Gets or sets the assembly type that contains the job to run.
        /// </summary>
        [ConfigurationProperty("type", IsRequired = true)]
        public string Type
        {
            get { return (string) this["type"]; }
            set { this["type"] = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to keep alive the instance between job runs.
        /// </summary>
        /// <value><c>true</c> to keep alive instance; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Setting this to true, the default value, will keep the <see cref="IJob"/> instance alive between runs.
        /// </remarks>
        [ConfigurationProperty("keepAlive", DefaultValue = true)]
        public bool KeepAlive
        {
            get { return (bool) this["keepAlive"]; }
            set { this["keepAlive"] = value; }
        }

        /// <summary>
        /// Gets or sets the name of the provider that is used to lock the job when running.
        /// </summary>
        /// <value>The type to use to lock the job.</value>
        [ConfigurationProperty("jobLockProvider")]
        public string JobLockProvider
        {
            get { return (string) this["jobLockProvider"]; }
            set { this["jobLockProvider"] = value; }
        }

        /// <summary>
        /// Gets or sets the job history provider.
        /// </summary>
        /// <value>The job history provider.</value>
        [ConfigurationProperty("jobHistoryProvider")]
        public string JobHistoryProvider
        {
            get { return (string)this["jobHistoryProvider"]; }
            set { this["jobHistoryProvider"] = value; }
        }

        /// <summary>
        /// Gets the arguments.
        /// </summary>
        /// <value>The arguments.</value>
        public IDictionary<string, object> Arguments
        {
            get { return _arguments; }
        }

        /// <summary>
        /// Gets a value indicating whether an unknown attribute is encountered during deserialization.
        /// </summary>
        /// <param name="name">The name of the unrecognized attribute.</param>
        /// <param name="value">The value of the unrecognized attribute.</param>
        /// <returns>
        /// true when an unknown attribute is encountered while deserializing; otherwise, false.
        /// </returns>
        protected override bool OnDeserializeUnrecognizedAttribute(string name, string value)
        {
            _arguments.Add(name, value);
            return true;
        }
    }
}