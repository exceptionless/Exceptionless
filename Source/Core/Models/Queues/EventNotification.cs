#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Core.Utility;
using Exceptionless.Models;

namespace Exceptionless.Core.Queues.Models {
    public class EventNotification : ExtensibleObject {
        public PersistentEvent Event { get; set; }
        public bool IsNew { get; set; }
        public bool IsCritical { get; set; }
        public bool IsRegression { get; set; }
        public int TotalOccurrences { get; set; }
        public string ProjectName { get; set; }
    }
}