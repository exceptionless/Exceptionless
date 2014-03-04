#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Models;
using UAParser;

namespace Exceptionless.App.Models.Error {
    public class ErrorModel : Exceptionless.Models.Error {
        public virtual void PopulateExtraInfo() {
            if (RequestInfo != null && RequestInfo.UserAgent != null) {
                Parser parser = Parser.GetDefault();
                UserAgentInfo = parser.Parse(RequestInfo.UserAgent);
            }

            StackingInfo st = this.GetStackingInfo();
            StackingType = st.FullTypeName;
            StackingMethod = st.MethodName;
            StackingMessage = st.Message;
            StackingExtendedData = st.Error.ExtendedData;
        }

        public DateTimeOffset ClientTime { get; set; }
        public ClientInfo UserAgentInfo { get; set; }
        public string PreviousErrorId { get; set; }
        public string NextErrorId { get; set; }
        public HashSet<string> PromotedTabs { get; set; }
        public string CustomContent { get; set; }
        public string StackingType { get; set; }
        public string StackingMethod { get; set; }
        public string StackingMessage { get; set; }
        public DataDictionary StackingExtendedData { get; set; }
    }
}