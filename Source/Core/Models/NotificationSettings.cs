using System;

namespace Exceptionless.Core.Models {
    public class NotificationSettings {
        public bool SendDailySummary { get; set; }
        public bool ReportNewErrors { get; set; }
        public bool ReportCriticalErrors { get; set; }
        public bool ReportEventRegressions { get; set; }
        public bool ReportNewEvents { get; set; }
        public bool ReportCriticalEvents { get; set; }
    }
}