using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Models;
using MongoDB.Driver;

namespace Exceptionless.Core.Repositories {
    public class MongoRepositoryOwnedByProjectAndStack<T> : MongoRepositoryOwnedByProject<T>, IRepositoryOwnedByStack<T> where T : class, IOwnedByProject, IIdentity, IOwnedByStack, new() {
        public MongoRepositoryOwnedByProjectAndStack(MongoDatabase database, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(database, cacheClient, messagePublisher) {}

        protected override void BeforeAdd(IList<T> documents) {
            if (documents.Any(d => String.IsNullOrEmpty(d.StackId)))
                throw new ArgumentException("StackIds must be set.");

            base.BeforeAdd(documents);
        }

        public IList<T> GetByStackId(string stackId, bool useCache = false, TimeSpan? expiresIn = null) {
            return Find<T>(new FindMultipleOptions().WithOrganizationId(stackId).WithCacheKey(useCache ? String.Concat("stack:", stackId) : null).WithExpiresIn(expiresIn));
        }

        public IList<T> GetByStackId(IList<string> stackIds, bool useCache = false, TimeSpan? expiresIn = null) {
            if (stackIds == null || stackIds.Count == 0)
                return new List<T>();

            string cacheKey = String.Concat("stack:", String.Join("", stackIds).GetHashCode().ToString());
            return Find<T>(new FindMultipleOptions().WithOrganizationIds(stackIds).WithCacheKey(useCache ? cacheKey : null).WithExpiresIn(expiresIn));
        }

        public void RemoveAllByStackId(string stackId) {
            RemoveAll(new QueryOptions().WithStackId(stackId));
        }

        public async Task RemoveAllByStackIdAsync(string stackId) {
            await Task.Run(() => RemoveAllByStackId(stackId));
        }
    }
}
