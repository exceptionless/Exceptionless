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

namespace Exceptionless.Core {
    public interface IEventRepository : IRepositoryOwnedByOrganization<PersistentEvent> {
        IEnumerable<PersistentEvent> GetMostRecent(string projectId, DateTime utcStart, DateTime utcEnd, int? skip, int? take, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true);

        IEnumerable<PersistentEvent> GetByStackIdOccurrenceDate(string stackId, DateTime utcStart, DateTime utcEnd, int? skip, int? take);

        string GetPreviousEventIdInStack(string id);

        string GetNextEventIdInStack(string id);

        void RemoveAllByProjectId(string projectId);
    }
}