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

namespace Exceptionless.Models {
    public class EventStatsWithStackIds : ErrorStats {
        public EventStatsWithStackIds() {
            StackIds = new Dictionary<string, int>();
            NewStackIds = new List<string>();
        }

        public Dictionary<string, int> StackIds { get; set; }
        public List<string> NewStackIds { get; set; }

        public override string ToString() {
            return String.Format("Total: {0} New: {1} Unique: {2}", Total, NewTotal, StackIds.Count);
        }
    }
}