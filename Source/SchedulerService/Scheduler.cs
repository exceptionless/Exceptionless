using System;
using System.ServiceProcess;
using CodeSmith.Core.Scheduler;

namespace SchedulerService {
    public partial class Scheduler : ServiceBase {
        public Scheduler() {
            InitializeComponent();
        }

        protected override void OnStart(string[] args) {
            JobManager.Current.Start();
        }

        protected override void OnStop() {
            JobManager.Current.Stop();
        }
    }
}
