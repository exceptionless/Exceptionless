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
    public static class StackQueryExtensions {
        internal const string StacksKey = "@Stacks";
        internal const string ExcludedStacksKey = "@ExcludedStacks";

        public static T Stack<T>(this T query, string stackId) where T : IRepositoryQuery {
            return query.AddCollectionOptionValue(StacksKey, stackId);
        }
        
        public static T Stack<T>(this T query, IEnumerable<string> stackIds) where T : IRepositoryQuery {
            return query.AddCollectionOptionValue(StacksKey, stackIds.Distinct());
        }

        public static T ExcludeStack<T>(this T query, string stackId) where T : IRepositoryQuery {
            return query.AddCollectionOptionValue(ExcludedStacksKey, stackId);
        }

        public static T ExcludeStack<T>(this T query, IEnumerable<string> stackIds) where T : IRepositoryQuery {
            return query.AddCollectionOptionValue(ExcludedStacksKey, stackIds);
        }
    }
}

namespace Exceptionless.Core.Repositories.Options {
    public static class ReadStackQueryExtensions {
        public static ICollection<string> GetStacks(this IRepositoryQuery query) {
            return query.SafeGetCollection<string>(StackQueryExtensions.StacksKey);
        }

        public static ICollection<string> GetExcludedStacks(this IRepositoryQuery query) {
            return query.SafeGetCollection<string>(StackQueryExtensions.ExcludedStacksKey);
        }
    }
}

namespace Exceptionless.Core.Repositories.Queries {
    public class StackQueryBuilder : IElasticQueryBuilder {
        private readonly string _stackIdFieldName;

        public StackQueryBuilder() {
            _stackIdFieldName = nameof(IOwnedByStack.StackId).ToLowerUnderscoredWords();
        }

        public Task BuildAsync<T>(QueryBuilderContext<T> ctx) where T : class, new() {
            var stackIds = ctx.Source.GetStacks();
            var excludedStackIds = ctx.Source.GetExcludedStacks();

            if (stackIds.Count == 1)
                ctx.Filter &= Query<T>.Term(_stackIdFieldName, stackIds.Single());
            else if (stackIds.Count > 1)
                ctx.Filter &= Query<T>.Terms(d => d.Field(_stackIdFieldName).Terms(stackIds));

            if (excludedStackIds.Count == 1)
                ctx.Filter &= Query<T>.Bool(b => b.MustNot(Query<T>.Term(_stackIdFieldName, excludedStackIds.Single())));
            else if (excludedStackIds.Count > 1)
                ctx.Filter &= Query<T>.Bool(b => b.MustNot(Query<T>.Terms(d => d.Field(_stackIdFieldName).Terms(excludedStackIds))));

            return Task.CompletedTask;
        }
    }
}