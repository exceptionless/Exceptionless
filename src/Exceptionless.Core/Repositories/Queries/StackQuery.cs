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
    public static class StackQueryExtensions {
        internal const string StacksKey = "@Stacks";

        public static T Stack<T>(this T query, string stackId) where T : IRepositoryQuery {
            return query.AddCollectionOptionValue(StacksKey, stackId);
        }
    }
}

namespace Exceptionless.Core.Repositories.Options {
    public static class ReadStackQueryExtensions {
        public static ICollection<string> GetStacks(this IRepositoryQuery query) {
            return query.SafeGetCollection<string>(StackQueryExtensions.StacksKey);
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
            if (stackIds.Count <= 0)
                return Task.CompletedTask;

            if (stackIds.Count == 1)
                ctx.Filter &= Query<T>.Term(_stackIdFieldName, stackIds.Single());
            else
                ctx.Filter &= Query<T>.Terms(d => d.Field(_stackIdFieldName).Terms(stackIds));

            return Task.CompletedTask;
        }
    }
}