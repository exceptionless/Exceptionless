using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories {
    public interface IStackRepository : IRepositoryOwnedByOrganizationAndProject<Stack> {
        Stack GetStackBySignatureHash(string projectId, string signatureHash);
        ICollection<Stack> GetMostRecent(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, string query = null);
        ICollection<Stack> GetNew(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, string query = null);
        ICollection<Stack> GetByFilter(string systemFilter, string userFilter, string sort, SortOrder sortOrder, string field, DateTime utcStart, DateTime utcEnd, PagingOptions paging);

        void MarkAsRegressed(string stackId);
        void IncrementEventCounter(string organizationId, string projectId, string stackId, DateTime minOccurrenceDateUtc, DateTime maxOccurrenceDateUtc, int count, bool sendNotifications = true);
        void InvalidateCache(string projectId, string stackId, string signatureHash);
    }
}