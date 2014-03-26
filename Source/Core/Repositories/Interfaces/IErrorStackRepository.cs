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
    public interface IErrorStackRepository : IRepositoryOwnedByOrganization<ErrorStack> {
        ErrorStackInfo GetErrorStackInfoBySignatureHash(string projectId, string signatureHash);

        IEnumerable<ErrorStack> GetMostRecent(string projectId, DateTime utcStart, DateTime utcEnd, int? skip, int? take, out long count, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true);

        IEnumerable<ErrorStack> GetNew(string projectId, DateTime utcStart, DateTime utcEnd, int? skip, int? take, out long count, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true);

        void InvalidateHiddenIdsCache(string projectId);

        string[] GetFixedIds(string projectId);

        void InvalidateFixedIdsCache(string projectId);

        string[] GetNotFoundIds(string projectId);

        void InvalidateNotFoundIdsCache(string projectId);

        void RemoveAllByProjectId(string projectId);
    }

    public class ErrorStackInfo {
        public string Id { get; set; }
        public DateTime? DateFixed { get; set; }
        public bool OccurrencesAreCritical { get; set; }
        public string SignatureHash { get; set; }
        public bool IsHidden { get; set; }
    }
}