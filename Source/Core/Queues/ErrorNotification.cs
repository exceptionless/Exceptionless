#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;

namespace Exceptionless.Core.Queues {
    public class ErrorNotification {
        public string ErrorStackId { get; set; }
        public string ErrorId { get; set; }
        public string ProjectId { get; set; }
        public bool IsNew { get; set; }
        public bool IsCritical { get; set; }
        public bool IsRegression { get; set; }
        public string FullTypeName { get; set; }

        public string TypeName {
            get {
                if (String.IsNullOrEmpty(FullTypeName))
                    return String.Empty;

                string[] parts = FullTypeName.Split('.');
                return parts[parts.Length - 1];
            }
        }

        public string Message { get; set; }
        public string Url { get; set; }
        public string Code { get; set; }
        public string UserAgent { get; set; }
    }
}