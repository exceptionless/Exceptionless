using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    public class RepositoryOwnedByOrganizationAndProject<T> : RepositoryOwnedByOrganization<T>, IRepositoryOwnedByProject<T> where T : class, IOwnedByProject, IIdentity, IOwnedByOrganization, new() {
        public RepositoryOwnedByOrganizationAndProject(ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(cacheClient, messagePublisher) {}

        protected override void BeforeAdd(ICollection<T> documents) {
            if (documents.Any(d => String.IsNullOrEmpty(d.ProjectId)))
                throw new ArgumentException("ProjectIds must be set.");

            base.BeforeAdd(documents);
        }

        public virtual ICollection<T> GetByProjectId(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            return Find<T>(FindOptionsExtensions.WithPaging(new MultiOptions()
                    .WithProjectId(projectId), paging)
                .WithCacheKey(useCache ? String.Concat("project:", projectId) : null)
                .WithExpiresIn(expiresIn));
        }

        public async Task RemoveAllByProjectIdAsync(string projectId) {
            await Task.Run(() => RemoveAll(new QueryOptions().WithProjectId(projectId)));
        }
    }
}