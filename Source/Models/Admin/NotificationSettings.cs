#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;

namespace Exceptionless.Models {
    public class NotificationSettings {
        public NotificationMode Mode { get; set; }
        public bool SendDailySummary { get; set; }
        public bool ReportCriticalErrors { get; set; }
        public bool ReportRegressions { get; set; }
        public bool Report404Errors { get; set; }
        public bool ReportKnownBotErrors { get; set; }
    }
}