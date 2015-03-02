using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    public class RepositoryOwnedByOrganizationAndProjectAndStack<T> : RepositoryOwnedByOrganizationAndProject<T>, IRepositoryOwnedByStack<T> where T : class, IOwnedByProject, IIdentity, IOwnedByStack, IOwnedByOrganization, new() {
        public RepositoryOwnedByOrganizationAndProjectAndStack(ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(cacheClient, messagePublisher) {}

        protected override void BeforeAdd(ICollection<T> documents) {
            if (documents.Any(d => String.IsNullOrEmpty(d.StackId)))
                throw new ArgumentException("StackIds must be set.");

            base.BeforeAdd(documents);
        }

        public virtual ICollection<T> GetByStackId(string stackId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return Find<T>(FindOptionsExtensions.WithPaging(new MultiOptions()
                    .WithStackId(stackId), paging)
                .WithCacheKey(useCache ? String.Concat("stack:", stackId) : null)
                .WithExpiresIn(expiresIn));
        }

        public async Task RemoveAllByStackIdAsync(string stackId) {
            await Task.Run(() => RemoveAll(new QueryOptions().WithStackId(stackId)));
        }
    }
}