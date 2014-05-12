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
using Exceptionless.Models;

namespace Exceptionless.Core.Repositories {
    public interface IEventRepository : IRepositoryOwnedByOrganizationAndProjectAndStack<PersistentEvent> {
        IList<PersistentEvent> GetMostRecent(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true);
        IList<PersistentEvent> GetByStackIdOccurrenceDate(string stackId, DateTime utcStart, DateTime utcEnd, PagingOptions paging);
        IList<string> GetExceededRetentionEventIds(string stackId, int maxEventsPerStack);
        string GetPreviousEventIdInStack(string id);
        string GetNextEventIdInStack(string id);
    }
}