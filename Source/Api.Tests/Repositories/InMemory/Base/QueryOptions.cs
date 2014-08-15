using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Exceptionless.Models;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    public class QueryOptions<T> where T: IIdentity {
        protected readonly static bool _isOwnedByOrganization = typeof(IOwnedByOrganization).IsAssignableFrom(typeof(T));
        protected readonly static bool _isOwnedByProject = typeof(IOwnedByProject).IsAssignableFrom(typeof(T));
        protected readonly static bool _isOwnedByStack = typeof(IOwnedByStack).IsAssignableFrom(typeof(T));

        public QueryOptions() {
            Ids = new List<string>();
            OrganizationIds = new List<string>();
            ProjectIds = new List<string>();
            StackIds = new List<string>();
        }

        public List<string> Ids { get; set; }
        public List<string> OrganizationIds { get; set; }
        public List<string> ProjectIds { get; set; }
        public List<string> StackIds { get; set; }
        public Expression<Func<T, bool>> Query { get; set; }

        public virtual IQueryable<T> ApplyFilter(IQueryable<T> query) {
            query = query.Where(Query);
            if (Ids.Count > 0)
                query = query.Where(d => Ids.Contains(d.Id));
            if (_isOwnedByOrganization && OrganizationIds.Count > 0)
                query = query.Where(d => OrganizationIds.Contains(((IOwnedByOrganization)d).OrganizationId));
            if (_isOwnedByProject && ProjectIds.Count > 0)
                query = query.Where(d => ProjectIds.Contains(((IOwnedByProject)d).ProjectId));
            if (_isOwnedByStack && StackIds.Count > 0)
                query = query.Where(d => StackIds.Contains(((IOwnedByStack)d).StackId));

            return query;
        }
    }
}
