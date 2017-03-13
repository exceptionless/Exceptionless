using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Options;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Options;
using Nest;

namespace Exceptionless.Core.Repositories {
    public static class OrganizationQueryExtensions {
        internal const string OrganizationsKey = "@Organizations";

        public static T Organization<T>(this T query, string organizationId) where T : IRepositoryQuery {
            return query.AddCollectionOptionValue(OrganizationsKey, organizationId);
        }

        public static T Organizations<T>(this T query, IEnumerable<string> organizationIds) where T : IRepositoryQuery {
            return query.AddCollectionOptionValue(OrganizationsKey, organizationIds.Distinct());
        }
    }
}

namespace Exceptionless.Core.Repositories.Options {
    public static class ReadOrganizationQueryExtensions {
        public static ICollection<string> GetOrganizations(this IRepositoryQuery query) {
            return query.SafeGetCollection<string>(OrganizationQueryExtensions.OrganizationsKey);
        }
    }
}

namespace Exceptionless.Core.Repositories.Queries {
    public class OrganizationQueryBuilder : IElasticQueryBuilder {
        private readonly string _organizationIdFieldName;

        public OrganizationQueryBuilder() {
            _organizationIdFieldName = nameof(IOwnedByOrganization.OrganizationId).ToLowerUnderscoredWords();
        }

        public Task BuildAsync<T>(QueryBuilderContext<T> ctx) where T : class, new() {
            var organizationIds = ctx.Source.GetOrganizations();
            if (organizationIds.Count <= 0)
                return Task.CompletedTask;

            if (organizationIds.Count == 1)
                ctx.Filter &= Query<T>.Term(_organizationIdFieldName, organizationIds.Single());
            else
                ctx.Filter &= Query<T>.Terms(d => d.Field(_organizationIdFieldName).Terms(organizationIds));

            return Task.CompletedTask;
        }
    }
}