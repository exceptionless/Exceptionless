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
    public interface IStackRepository : IRepositoryOwnedByOrganizationAndProject<Stack> {
        StackInfo GetStackInfoBySignatureHash(string projectId, string signatureHash);
        ICollection<Stack> GetMostRecent(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true);
        ICollection<Stack> GetNew(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true);
        void MarkAsRegressed(string stackId, string organizationId);
        void IncrementEventCounter(string stackId, string organizationId, DateTime occurrenceDate);
        void InvalidateCache(string id, string signatureHash, string projectId);
    }

    public class StackInfo {
        public string Id { get; set; }
        public DateTime? DateFixed { get; set; }
        public bool OccurrencesAreCritical { get; set; }
        public bool IsHidden { get; set; }
    }
}