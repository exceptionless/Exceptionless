using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using CodeSmith.Core.Dependency;
using CodeSmith.Core.Threading;

namespace CodeSmith.Core.Scheduler
{
    /// <summary>
    /// A class representing a scheduled job.
    /// </summary>
    public class JobRunner
    {
        #region Properties

        private readonly IDictionary<string, object> _arguments;
        private readonly TimeSpan _interval;
        private readonly bool _keepAlive;
        private readonly string _name;
        private readonly string _description;
        private readonly string _group;
        private readonly bool _isTimeOfDay;

        private readonly Synchronized<bool> _isBusy;
        private readonly Synchronized<string> _lastResult;
        private readonly Synchronized<DateTime> _lastRunStartTime;
        private readonly Synchronized<DateTime> _lastRunFinishTime;
        private readonly Synchronized<JobStatus> _lastStatus;
        private readonly Synchronized<DateTime> _nextRunTime;
        private readonly Synchronized<JobStatus> _status;


        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Gets the description.
        /// </summary>
        /// <value>The description.</value>
        public string Description
        {
            get { return _description; }
        }

        /// <summary>
        /// Gets the group name.
        /// </summary>
        /// <value>The group name.</value>
        public string Group
        {
            get { return _group; }
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
        /// Gets the job interval.
        /// </summary>
        /// <value>The job interval.</value>
        public TimeSpan Interval
        {
            get { return _interval; }
        }

        /// <summary>
        /// Gets a value indicating whether Interval is a time of day.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if Interval is time of day; otherwise, <c>false</c>.
        /// </value>
        public bool IsTimeOfDay
        {
            get { return _isTimeOfDay; }
        }

        /// <summary>
        /// Gets a value indicating whether to keep alive the job instance.
        /// </summary>
        /// <value><c>true</c> to keep alive; otherwise, <c>false</c>.</value>
        public bool KeepAlive
        {
            get { return _keepAlive; }
        }

        /// <summary>
        /// Gets the last run start time.
        /// </summary>
        /// <value>The last run start time.</value>
        /// <remarks>This property is thread safe.</remarks>
        public DateTime LastRunStartTime
        {
            get { return _lastRunStartTime.Value; }
            set { _lastRunStartTime.Value = value; }
        }

        /// <summary>
        /// Gets the last run finish time.
        /// </summary>
        /// <value>The last run finish time.</value>
        /// <remarks>This property is thread safe.</remarks>
        public DateTime LastRunFinishTime
        {
            get { return _lastRunFinishTime.Value; }
            set { _lastRunFinishTime.Value = value; }
        }

        /// <summary>
        /// Gets the last run duration.
        /// </summary>
        /// <value>The last run duration.</value>
        /// <remarks>This property is thread safe.</remarks>
        public TimeSpan LastRunDuration
        {
            get { return _lastRunFinishTime.Value.Subtract(_lastRunStartTime.Value); }
        }

        /// <summary>
        /// Gets the next run time.
        /// </summary>
        /// <value>The next run time.</value>
        /// <remarks>This property is thread safe.</remarks>
        public DateTime NextRunTime
        {
            get { return _nextRunTime.Value; }
            private set { _nextRunTime.Value = value; }
        }

        /// <summary>
        /// Gets the status.
        /// </summary>
        /// <value>The status.</value>
        /// <remarks>This property is thread safe.</remarks>
        public JobStatus Status
        {
            get { return _status.Value; }
            private set { _status.Value = value; }
        }

        /// <summary>
        /// Gets the last status.
        /// </summary>
        /// <value>The last status.</value>
        /// <remarks>This property is thread safe.</remarks>
        public JobStatus LastStatus
        {
            get { return _lastStatus.Value; }
            set { _lastStatus.Value = value; }
        }

        /// <summary>
        /// Gets the last result.
        /// </summary>
        /// <value>The last result.</value>
        /// <remarks>This property is thread safe.</remarks>
        public string LastResult
        {
            get { return _lastResult.Value; }
            set { _lastResult.Value = value; }
        }

        /// <summary>
        /// Gets a value indicating whether this job is busy.
        /// </summary>
        /// <value><c>true</c> if this job is busy; otherwise, <c>false</c>.</value>
        /// <remarks>This property is thread safe.</remarks>
        public bool IsBusy
        {
            get { return _isBusy.Value; }
            private set { _isBusy.Value = value; }
        }

        #endregion

        private readonly string _id;
        private readonly JobLockProvider _jobLockProvider;
        private readonly JobHistoryProvider _jobHistoryProvider;
        private readonly IDependencyResolver _dependencyResolver;
        private readonly Type _jobType;
        private readonly object _runLock;
        private readonly Timer _timer;
        private IJob _instance;
        private CancellationTokenSource _tokenSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="JobRunner"/> class.
        /// </summary>
        public JobRunner(IJobConfiguration configuration, Type jobType, JobLockProvider jobLockProvider, JobHistoryProvider jobHistoryProvider, IDependencyResolver dependencyResolver)
        {
            _id = Guid.NewGuid().ToString("N").Substring(0, 10).ToLower();
            _isBusy = new Synchronized<bool>();
            _lastResult = new Synchronized<string>();
            _lastRunStartTime = new Synchronized<DateTime>();
            _lastRunFinishTime = new Synchronized<DateTime>();
            _lastStatus = new Synchronized<JobStatus>();
            _nextRunTime = new Synchronized<DateTime>();
            _status = new Synchronized<JobStatus>();
            _runLock = new object();
            _name = configuration.Name;
            _description = configuration.Description;
            _group = configuration.Group;
            _interval = configuration.Interval;
            _isTimeOfDay = configuration.IsTimeOfDay;
            _keepAlive = configuration.KeepAlive;
            _arguments = configuration.Arguments;

            _jobType = jobType;
            _jobLockProvider = jobLockProvider ?? new DefaultJobLockProvider();
            _dependencyResolver = dependencyResolver ?? new DefaultDependencyResolver();
            _jobHistoryProvider = jobHistoryProvider;

            _instance = null;
            _timer = new Timer(OnTimerCallback);

            if (_jobHistoryProvider != null)
                _jobHistoryProvider.RestoreHistory(this);

            Trace.TraceInformation("Job {0} created on {1}.", Name, Environment.MachineName);
        }

        /// <summary>
        /// Starts this job timer.
        /// </summary>
        public void Start()
        {
            if (IsBusy)
                return;

            JobManager.Current.OnJobStarting(new JobEventArgs(Name, JobAction.Starting, _id));

            StartTimer();

            Status = JobStatus.Waiting;
        }

        /// <summary>
        /// Stops this job timer.
        /// </summary>
        public void Stop()
        {
            Stop(false);
        }

        /// <summary>
        /// Stops this job timer.
        /// </summary>
        /// <param name="cancel">if set to <c>true</c> cancel running job.</param>
        public void Stop(bool cancel)
        {
            JobManager.Current.OnJobStopping(new JobEventArgs(Name, JobAction.Stopping, _id));

            StopTimer();

            if (cancel && IsBusy && _instance != null)
                _tokenSource.Cancel();

            Status = JobStatus.Stopped;
        }

        /// <summary>
        /// Runs this job asynchronous.
        /// </summary>
        /// <remarks>Can be used to speed up the job when an event occurs.</remarks>
        public void RunAsync()
        {
            // use the timer to run async
            Run(TimeSpan.FromMilliseconds(30));
        }

        /// <summary>
        /// Runs this job at the specified time.
        /// </summary>
        /// <param name="runTime">The run time.</param>
        /// <remarks>Can be used to speed up the job when an event occurs.</remarks>
        public void Run(TimeSpan runTime)
        {
            if (IsBusy)
                return;

            NextRunTime = DateTime.Now.Add(runTime);
            _timer.Change(runTime, TimeSpan.Zero);

            Status = JobStatus.Waiting;
        }

        /// <summary>
        /// Runs this job.
        /// </summary>
        public void Run()
        {
            if (IsBusy)
                return;

            lock (_runLock)
                RunInternal();
        }

        private void RunInternal()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            // get lock
            using (var jobLock = _jobLockProvider.Acquire(Name))
            {
                // give up if no lock
                if (!jobLock.LockAcquired) {
                    LastResult = "Could not acquire a job lock.";
                    LastStatus = JobStatus.Canceled;
                    Status = JobStatus.Waiting;
                    IsBusy = false;

                    return;
                }

                DateTime started = DateTime.Now;
                LastRunStartTime = started;
                JobManager.Current.OnJobRunning(new JobEventArgs(Name, JobAction.Running, _id));

                Status = JobStatus.Running;
                Interlocked.Increment(ref JobManager.JobsRunning);

                try
                {
                    CreateInstance();

                    var context = new JobRunContext(UpdateStatus);
                    _tokenSource = new CancellationTokenSource();
                    var task = _instance.RunAsync(context, _tokenSource.Token);
                    task.Wait();
                    JobResult r = task.Result;
                    if (task.IsFaulted && task.Exception != null) {
                        LastResult = task.Exception.Message;
                    }
                    else if (r == null)
                    {
                        if (String.IsNullOrEmpty(LastResult))
                            LastResult = "Completed";
                        LastStatus = JobStatus.Completed;
                    }
                    else if (r.Error != null)
                    {
                        LastResult = r.Error.Message;
                        LastStatus = JobStatus.Error;

                        Trace.TraceError(r.Error.ToString());
                    }
                    else
                    {
                        if (r.Message != null)
                            LastResult = r.Message.ToString();
                        LastStatus = JobStatus.Completed;
                    }
                }
                catch (Exception ex)
                {
                    LastResult = ex.Message;
                    LastStatus = JobStatus.Error;

                    Trace.TraceError(ex.ToString());
                }
                finally
                {
                    Interlocked.Decrement(ref JobManager.JobsRunning);
                    LastRunFinishTime = DateTime.Now;

                    if (!_keepAlive)
                        _instance = null;

                    try
                    {
                        if (_jobHistoryProvider != null)
                            _jobHistoryProvider.SaveHistory(this);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("Error saving job history: " + ex.Message);
                    }

                    JobManager.Current.OnJobCompleted(
                        new JobCompletedEventArgs(
                            Name,
                            JobAction.Completed,
                            _id,
                            started,
                            LastRunFinishTime,
                            LastResult,
                            LastStatus));

                    Status = JobStatus.Waiting;
                    IsBusy = false;
                }
            } // release job lock
        }

        private void CreateInstance()
        {
            if (_instance == null)
                _instance = _dependencyResolver.GetService(_jobType) as IJob;

            if (_instance == null)
                throw new InvalidOperationException(String.Format(
                    "Could not create an instance of '{0}'.", _jobType.Name));
        }

        public IJob Instance
        {
            get
            {
                CreateInstance();
                return _instance;
            }
        }

        private void UpdateStatus(string message)
        {
            LastResult = message;
        }

        private void StartTimer()
        {
            if (!IsTimeOfDay)
            {
                NextRunTime = DateTime.Now.Add(_interval);
                _timer.Change(_interval, TimeSpan.Zero);
                return;
            }

            // calculate the exact DateTime to run
            DateTime current = DateTime.Now;
            DateTime next = new DateTime(current.Year, current.Month, current.Day,
                                         _interval.Hours, _interval.Minutes, _interval.Seconds);

            // if job hasn't run in 1 day, run right away
            if (LastRunStartTime != DateTime.MinValue && current.Subtract(LastRunStartTime).TotalDays > 1.0)
            {
                next = DateTime.Now.AddSeconds(10);
            }
            // if next time already past, add 1 day to run tomorrow
            else if (next < current)
            {
                current = current.AddDays(1);

                next = new DateTime(current.Year, current.Month, current.Day,
                                    _interval.Hours, _interval.Minutes, _interval.Seconds);
            }

            NextRunTime = next;
            _timer.Change(next.Subtract(DateTime.Now), TimeSpan.Zero);
        }

        private void StopTimer()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void OnTimerCallback(object state)
        {
            try
            {
                StopTimer();
                Run();
            }
            finally
            {
                if (!IsBusy)
                {
                    StartTimer();
                    Status = JobStatus.Waiting;
                }
            }
        }
    }
}