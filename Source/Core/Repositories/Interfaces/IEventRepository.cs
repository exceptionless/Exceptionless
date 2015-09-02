﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories {
    public interface IEventRepository : IRepositoryOwnedByOrganizationAndProjectAndStack<PersistentEvent> {
        ICollection<PersistentEvent> GetMostRecent(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true);
        ICollection<PersistentEvent> GetByStackIdOccurrenceDate(string stackId, DateTime utcStart, DateTime utcEnd, PagingOptions paging);
        ICollection<PersistentEvent> GetByReferenceId(string projectId, string referenceId);
        ICollection<PersistentEvent> GetByFilter(string systemFilter, string userFilter, string sort, SortOrder sortOrder, string field, DateTime utcStart, DateTime utcEnd, PagingOptions paging);

        string GetPreviousEventId(string id, string systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null);
        string GetPreviousEventId(PersistentEvent ev, string systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null);
        string GetNextEventId(string id, string systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null);
        string GetNextEventId(PersistentEvent ev, string systemFilter = null, string userFilter = null, DateTime? utcStart = null, DateTime? utcEnd = null);
        void MarkAsRegressedByStack(string organizationId, string stackId);
        void UpdateFixedByStack(string organizationId, string stackId, bool value);
        void UpdateHiddenByStack(string organizationId, string stackId, bool value);
        void RemoveOldestEvents(string stackId, int maxEventsPerStack);
        void RemoveAllByDate(string organizationId, DateTime utcCutoffDate);
        void HideAllByClientIpAndDate(string organizationId, string clientIp, DateTime utcStartDate, DateTime utcEndDate);
        Task HideAllByClientIpAndDateAsync(string organizationId, string clientIp, DateTime utcStartDate, DateTime utcEndDate);

        ICollection<PersistentEvent> GetByOrganizationIds(ICollection<string> organizationIds, string query = null, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);

        long GetCountByOrganizationId(string organizationId);
        long GetCountByProjectId(string projectId);
        long GetCountByStackId(string stackId);
    }
}