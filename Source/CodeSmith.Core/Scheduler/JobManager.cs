using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Configuration.Provider;
using System.Diagnostics;
using System.Threading;
using CodeSmith.Core.Dependency;

namespace CodeSmith.Core.Scheduler {
    /// <summary>
    /// A class to manage the jobs for the Scheduler.
    /// </summary>
    public class JobManager : IDisposable {
        private static readonly object _initLock = new object();
        internal static int JobsRunning;

        private readonly string _id;
        private readonly Dictionary<JobProvider, JobCollection> _providerJobs;
        private readonly JobLockProviderCollection _jobLockProviders;
        private readonly JobLockProvider _defaultJobLockProvider;
        private readonly JobProviderCollection _jobProviders;
        private readonly JobCollection _jobs;
        private readonly Timer _jobProviderTimer;
        private IDependencyResolver _dependencyResolver;

        private bool _isInitialized;
        private DateTime _lastInitialize;

        #region Events
        /// <summary>
        /// Occurs when the JobManager starts.
        /// </summary>
        /// <seealso cref="Start"/>
        public event EventHandler<JobEventArgs> JobManagerStarting;

        /// <summary>
        /// Raises the <see cref="E:CodeSmith.Core.Scheduler.JobManager.JobManagerStarting"/> event.
        /// </summary>
        /// <param name="e">The <see cref="CodeSmith.Core.Scheduler.JobEventArgs"/> instance containing the event data.</param>
        private void OnJobManagerStarting(JobEventArgs e) {
            if (JobManagerStarting == null)
                return;

            JobManagerStarting(this, e);
        }

        /// <summary>
        /// Occurs after the JobManager starts.
        /// </summary>
        /// <seealso cref="Start"/>
        public event EventHandler<JobEventArgs> JobManagerStarted;

        /// <summary>
        /// Raises the <see cref="E:CodeSmith.Core.Scheduler.JobManager.JobManagerStarted"/> event.
        /// </summary>
        /// <param name="e">The <see cref="CodeSmith.Core.Scheduler.JobEventArgs"/> instance containing the event data.</param>
        private void OnJobManagerStarted(JobEventArgs e) {
            if (JobManagerStarted == null)
                return;

            JobManagerStarted(this, e);
        }

        /// <summary>
        /// Occurs when the JobManager stops.
        /// </summary>
        /// <seealso cref="Stop"/>
        public event EventHandler<JobEventArgs> JobManagerStopping;

        /// <summary>
        /// Raises the <see cref="E:CodeSmith.Core.Scheduler.JobManager.JobManagerStopping"/> event.
        /// </summary>
        /// <param name="e">The <see cref="CodeSmith.Core.Scheduler.JobEventArgs"/> instance containing the event data.</param>
        private void OnJobManagerStopping(JobEventArgs e) {
            if (JobManagerStopping == null)
                return;

            JobManagerStopping(this, e);
        }

        /// <summary>
        /// Occurs when the Job is starting.
        /// </summary>
        /// <seealso cref="M:CodeSmith.Core.Scheduler.Job.Start"/>
        public event EventHandler<JobEventArgs> JobStarting;

        /// <summary>
        /// Raises the <see cref="E:CodeSmith.Core.Scheduler.JobManager.JobStarting"/> event.
        /// </summary>
        /// <param name="e">The <see cref="CodeSmith.Core.Scheduler.JobEventArgs"/> instance containing the event data.</param>
        internal void OnJobStarting(JobEventArgs e) {
            if (JobStarting == null)
                return;

            JobStarting(this, e);
        }

        /// <summary>
        /// Occurs when the Job is stopping.
        /// </summary>
        /// <seealso cref="M:CodeSmith.Core.Scheduler.Job.Stop"/>
        public event EventHandler<JobEventArgs> JobStopping;

        /// <summary>
        /// Raises the <see cref="E:CodeSmith.Core.Scheduler.JobManager.JobStopping"/> event.
        /// </summary>
        /// <param name="e">The <see cref="CodeSmith.Core.Scheduler.JobEventArgs"/> instance containing the event data.</param>
        internal void OnJobStopping(JobEventArgs e) {
            if (JobStopping == null)
                return;

            JobStopping(this, e);
        }

        /// <summary>
        /// Occurs when the Job is running.
        /// </summary>
        /// <seealso cref="M:CodeSmith.Core.Scheduler.Job.Run"/>
        public event EventHandler<JobEventArgs> JobRunning;

        /// <summary>
        /// Raises the <see cref="E:CodeSmith.Core.Scheduler.JobManager.JobRunning"/> event.
        /// </summary>
        /// <param name="e">The <see cref="CodeSmith.Core.Scheduler.JobEventArgs"/> instance containing the event data.</param>
        internal void OnJobRunning(JobEventArgs e) {
            if (JobRunning == null)
                return;

            JobRunning(this, e);
        }

        /// <summary>
        /// Occurs when the Job run is completed.
        /// </summary>
        /// <seealso cref="M:CodeSmith.Core.Scheduler.Job.Run"/>
        public event EventHandler<JobCompletedEventArgs> JobCompleted;

        /// <summary>
        /// Raises the <see cref="E:CodeSmith.Core.Scheduler.JobManager.JobCompleted"/> event.
        /// </summary>
        /// <param name="e">The <see cref="JobCompletedEventArgs"/> instance containing the event data.</param>
        internal void OnJobCompleted(JobCompletedEventArgs e) {
            if (JobCompleted == null)
                return;

            JobCompleted(this, e);
        }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="JobManager"/> class.
        /// </summary>
        internal JobManager() {
            _id = Guid.NewGuid().ToString("N").Substring(0, 10).ToLower();
            _providerJobs = new Dictionary<JobProvider, JobCollection>();
            _jobLockProviders = new JobLockProviderCollection();
            _defaultJobLockProvider = new DefaultJobLockProvider();
            _jobProviders = new JobProviderCollection();
            _jobs = new JobCollection();
            _jobProviderTimer = new Timer(OnJobProviderCallback);
        }

        public void SetDependencyResolver(IDependencyResolver dependencyResolver) {
            if (_dependencyResolver != null && (dependencyResolver == _dependencyResolver || dependencyResolver.GetType() == _dependencyResolver.GetType()))
                return;

            if (_isInitialized)
                throw new InvalidOperationException("A dependency resolver can only be set before calling Start.");

            _dependencyResolver = dependencyResolver ?? new DefaultDependencyResolver();
        }

        /// <summary>
        /// Gets the number of active jobs.
        /// </summary>
        /// <value>The number of active jobs.</value>
        public int ActiveJobs {
            get { return JobsRunning; }
        }

        /// <summary>
        /// Gets the collection of jobs.
        /// </summary>
        /// <value>The collection of jobs.</value>
        public JobCollection Jobs {
            get { return _jobs; }
        }

        /// <summary>
        /// Initializes the jobs for this manager.
        /// </summary>
        public void Initialize() {
            if (_isInitialized)
                return;

            // thread safe init
            lock (_initLock) {
                if (_isInitialized)
                    return;

                if (_dependencyResolver == null)
                    _dependencyResolver = new DefaultDependencyResolver();

                var jobManager = ConfigurationManager.GetSection("jobManager") as JobManagerSection;
                if (jobManager == null)
                    throw new ConfigurationErrorsException("Could not find 'jobManager' section in app.config or web.config file.");

                //load lock providers
                foreach (ProviderSettings providerSettings in jobManager.JobLockProviders)
                    _jobLockProviders.Add(InstantiateProvider(providerSettings, typeof(JobLockProvider)));

                //add config jobs
                AddJobs(jobManager.Jobs, null);

                // add job providers
                foreach (ProviderSettings providerSettings in jobManager.JobProviders)
                    _jobProviders.Add(InstantiateProvider(providerSettings, typeof(JobProvider)));

                foreach (JobProvider jobProvider in _jobProviders)
                    AddJobs(jobProvider.GetJobs(), jobProvider);

                _jobProviderTimer.Change(jobManager.JobProviderPoll, jobManager.JobProviderPoll);

                _lastInitialize = DateTime.Now;
                _isInitialized = true;
            }
        }

        public ProviderBase InstantiateProvider(ProviderSettings providerSettings, Type providerType) {
            ProviderBase providerBase;
            try {
                string typeName = providerSettings.Type == null ? null : providerSettings.Type.Trim();
                if (String.IsNullOrEmpty(typeName))
                    throw new ArgumentException("Provider type can't be null or empty.");

                Type type = Type.GetType(typeName);
                if (!providerType.IsAssignableFrom(type))
                    throw new ArgumentException("Provider type must be assignable to target type.");

                providerBase = _dependencyResolver.GetService(type) as ProviderBase;
                NameValueCollection parameters = providerSettings.Parameters;
                var config = new NameValueCollection(parameters.Count, StringComparer.Ordinal);
                foreach (string index in parameters)
                    config[index] = parameters[index];
                providerBase.Initialize(providerSettings.Name, config);
            } catch (Exception ex) {
                if (!(ex is ConfigurationException))
                    throw new ConfigurationErrorsException(ex.Message, ex, providerSettings.ElementInformation.Properties["type"].Source, providerSettings.ElementInformation.Properties["type"].LineNumber);
                throw;
            }

            return providerBase;
        }

        private void OnJobProviderCallback(object state) {
            bool wasReloaded = false;

            // make thread safe by making sure this can't run when Initialize is running 
            lock (_initLock) {
                foreach (JobProvider provider in _jobProviders) {
                    if (!provider.IsReloadRequired(_lastInitialize))
                        continue;

                    Trace.TraceInformation("Reload jobs for provider {0}.", provider.ToString());

                    //reload this provider
                    JobCollection providerJobs;
                    if (!_providerJobs.TryGetValue(provider, out providerJobs)) {
                        providerJobs = new JobCollection();
                        _providerJobs.Add(provider, providerJobs);
                    }

                    //remove jobs
                    foreach (JobRunner job in providerJobs) {
                        job.Stop(true);
                        _jobs.Remove(job);
                    }
                    providerJobs.Clear();

                    //add jobs back
                    AddJobs(provider.GetJobs(), provider);
                    wasReloaded = true;

                    foreach (JobRunner job in providerJobs)
                        job.Start();
                }
            }

            if (wasReloaded)
                _lastInitialize = DateTime.Now;
        }

        private void AddJobs(IEnumerable<IJobConfiguration> jobs, JobProvider provider) {
            if (jobs == null)
                return;

            foreach (var jobConfiguration in jobs) {
                Type jobType = Type.GetType(jobConfiguration.Type, false, true);
                if (jobType == null)
                    throw new ConfigurationErrorsException(
                        String.Format("Could not load type '{0}' for job '{1}'.",
                            jobConfiguration.Type, jobConfiguration.Name));

                JobLockProvider jobLockProvider = _defaultJobLockProvider;

                if (!String.IsNullOrEmpty(jobConfiguration.JobLockProvider)) {
                    // first try getting from provider collection
                    jobLockProvider = _jobLockProviders[jobConfiguration.JobLockProvider];
                    if (jobLockProvider == null) {
                        // next, try loading type
                        Type lockType = Type.GetType(jobConfiguration.JobLockProvider, false, true);
                        if (lockType == null)
                            throw new ConfigurationErrorsException(
                                String.Format("Could not load job lock type '{0}' for job '{1}'.",
                                    jobConfiguration.JobLockProvider, jobConfiguration.Name));

                        jobLockProvider = _dependencyResolver.GetService<JobLockProvider>();
                    }

                    // if not found in provider collection or couldn't create type.
                    if (jobLockProvider == null)
                        throw new ConfigurationErrorsException(
                            String.Format("Could not find job lock provider '{0}' for job '{1}'.", jobConfiguration.JobLockProvider, jobConfiguration.Name));
                }

                JobHistoryProvider jobHistoryProvider = null;
                if (!String.IsNullOrEmpty(jobConfiguration.JobHistoryProvider)) {
                    Type historyType = Type.GetType(jobConfiguration.JobHistoryProvider, false, true);
                    if (historyType == null)
                        throw new ConfigurationErrorsException(
                            String.Format("Could not load job history type '{0}' for job '{1}'.", jobConfiguration.JobHistoryProvider, jobConfiguration.Name));

                    jobHistoryProvider = _dependencyResolver.GetService<JobHistoryProvider>();
                }

                var j = new JobRunner(jobConfiguration, jobType, jobLockProvider, jobHistoryProvider, _dependencyResolver);
                _jobs.Add(j);

                // keep track of jobs for providers so they can be sync'd later
                if (provider == null)
                    continue;

                JobCollection providerJobs;
                if (!_providerJobs.TryGetValue(provider, out providerJobs)) {
                    providerJobs = new JobCollection();
                    _providerJobs.Add(provider, providerJobs);
                }
                providerJobs.Add(j);
            }

        }

        /// <summary>
        /// Starts all jobs in this manager.
        /// </summary>
        public void Start() {
            Trace.TraceInformation("JobManager.Start called at {0} on Thread {1}.", DateTime.Now, Thread.CurrentThread.ManagedThreadId);
            OnJobManagerStarting(new JobEventArgs("{JobManager}", JobAction.Starting, _id));

            Initialize();

            lock (_initLock) {
                foreach (JobRunner j in _jobs)
                    j.Start();
            }

            OnJobManagerStarted(new JobEventArgs("{JobManager}", JobAction.Running, _id));
        }

        /// <summary>
        /// Stops all jobs in this manager.
        /// </summary>
        public void Stop() {
            Trace.TraceInformation("JobManager.Stop called at {0} on Thread {1}.", DateTime.Now, Thread.CurrentThread.ManagedThreadId);
            OnJobManagerStopping(new JobEventArgs("{JobManager}", JobAction.Stopping, _id));

            lock (_initLock) {
                if (_jobs != null) {
                    foreach (JobRunner j in _jobs) {
                        if (j != null)
                            j.Stop(true);
                    }
                }
            }

            // safe shutdown
            DateTime timeout = DateTime.Now.AddSeconds(10);
            while (JobsRunning > 0) {
                Thread.Sleep(300);
                // timeout
                if (timeout < DateTime.Now)
                    break;
            }
        }

        /// <summary>
        /// Manually run the job with the specified name.
        /// </summary>
        /// <param name="jobName"></param>
        public void RunJob(string jobName)
        {
            if (!_jobs.Contains(jobName))
                return;

            var job = _jobs[jobName];
            job.Run();
        }

        /// <summary>
        /// Starts the job manually with the specified name.
        /// </summary>
        /// <param name="jobName"></param>
        public void StartJob(string jobName)
        {
            if (!_jobs.Contains(jobName))
                return;

            var job = _jobs[jobName];
            job.RunAsync();
        }

        /// <summary>
        /// Reload by stopping all jobs and reloading configuration. All the jobs will be restarted after reload.
        /// </summary>
        public void Reload() {
            Reload(true);
        }

        /// <summary>
        /// Reload by stopping all jobs and reloading configuration.
        /// </summary>
        /// <param name="startAfter">if set to <c>true</c> start the jobs after reload.</param>
        public void Reload(bool startAfter) {
            // make sure all jobs are stopped
            Stop();

            // clearing collections
            lock (_initLock) {
                _jobLockProviders.Clear();
                _jobProviders.Clear();
                _jobs.Clear();
                _lastInitialize = DateTime.MinValue;
                _isInitialized = false;
            }

            if (startAfter)
                Start();
        }

        public JobCollection GetJobsByGroup(string group) {
            var jobs = new JobCollection();

            lock (_initLock) {
                foreach (var job in _jobs) {
                    if (job.Group == group)
                        jobs.Add(job);
                }
            }

            return jobs;
        }

        #region Singleton

        /// <summary>
        /// Gets the current instance of <see cref="JobManager"/>.
        /// </summary>
        /// <value>The current instance.</value>
        public static JobManager Current {
            get { return Nested.Current; }
        }

        private class Nested {
            // Explicit static constructor to tell C# compiler not to mark type as beforefieldinit
            static Nested() { }

            internal static readonly JobManager Current = new JobManager();
        }

        #endregion

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() {
            if (_jobs != null) {
                foreach (JobRunner j in _jobs) {
                    if (j != null)
                        j.Stop(true);
                }

                _jobs.Clear();
            }

            _jobProviderTimer.Dispose();
        }
    }
}