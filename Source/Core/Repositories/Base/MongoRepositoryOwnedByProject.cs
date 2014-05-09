using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Models;
using MongoDB.Driver;

namespace Exceptionless.Core.Repositories {
    public class MongoRepositoryOwnedByProject<T> : MongoRepository<T>, IRepositoryOwnedByProject<T> where T : class, IOwnedByProject, IIdentity, new() {
        public MongoRepositoryOwnedByProject(MongoDatabase database, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(database, cacheClient, messagePublisher) {}

        protected override void BeforeAdd(IList<T> documents) {
            if (documents.Any(d => String.IsNullOrEmpty(d.ProjectId)))
                throw new ArgumentException("ProjectIds must be set.");

            base.BeforeAdd(documents);
        }

        public IList<T> GetByProjectId(string projectId, bool useCache = false, TimeSpan? expiresIn = null) {
            return Find<T>(new FindMultipleOptions().WithOrganizationId(projectId).WithCacheKey(useCache ? String.Concat("project:", projectId) : null).WithExpiresIn(expiresIn));
        }

        public IList<T> GetByProjectId(IList<string> projectIds, bool useCache = false, TimeSpan? expiresIn = null) {
            if (projectIds == null || projectIds.Count == 0)
                return new List<T>();

            string cacheKey = String.Concat("project:", String.Join("", projectIds).GetHashCode().ToString());
            return Find<T>(new FindMultipleOptions().WithOrganizationIds(projectIds).WithCacheKey(useCache ? cacheKey : null).WithExpiresIn(expiresIn));
        }

        public void RemoveAllByProjectId(string projectId) {
            RemoveAll(new QueryOptions().WithProjectId(projectId));
        }

        public async Task RemoveAllByProjectIdAsync(string projectId) {
            await Task.Run(() => RemoveAllByProjectId(projectId));
        }
    }
}
