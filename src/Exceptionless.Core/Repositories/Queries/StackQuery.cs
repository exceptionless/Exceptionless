using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Options;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Elasticsearch.Utility;
using Foundatio.Repositories.Options;

namespace Exceptionless.Core.Repositories
{
    public static class StackQueryExtensions
    {
        internal const string StacksKey = "@Stacks";
        internal const string ExcludedStacksKey = "@ExcludedStacks";
        internal const string SignatureHashesKey = "@SignatureHashes";

        public static T Stack<T>(this T query, string stackId) where T : IRepositoryQuery
        {
            return query.AddCollectionOptionValue(StacksKey, stackId);
        }

        public static T Stack<T>(this T query, IEnumerable<string> stackIds) where T : IRepositoryQuery
        {
            return query.AddCollectionOptionValue(StacksKey, stackIds.Distinct());
        }

        public static T ExcludeStack<T>(this T query, string stackId) where T : IRepositoryQuery
        {
            return query.AddCollectionOptionValue(ExcludedStacksKey, stackId);
        }

        public static T ExcludeStack<T>(this T query, IEnumerable<string> stackIds) where T : IRepositoryQuery
        {
            return query.AddCollectionOptionValue(ExcludedStacksKey, stackIds);
        }

        public static T SignatureHash<T>(this T query, IEnumerable<string> signatureHashes) where T : IRepositoryQuery
        {
            return query.AddCollectionOptionValue(SignatureHashesKey, signatureHashes.Distinct());
        }
    }
}

namespace Exceptionless.Core.Repositories.Options
{
    public static class ReadStackQueryExtensions
    {
        public static ICollection<string> GetStacks(this IRepositoryQuery query)
        {
            return query.SafeGetCollection<string>(StackQueryExtensions.StacksKey);
        }

        public static ICollection<string> GetExcludedStacks(this IRepositoryQuery query)
        {
            return query.SafeGetCollection<string>(StackQueryExtensions.ExcludedStacksKey);
        }


        public static ICollection<string> GetSignatureHashes(this IRepositoryQuery query)
        {
            return query.SafeGetCollection<string>(StackQueryExtensions.SignatureHashesKey);
        }
    }
}

namespace Exceptionless.Core.Repositories.Queries
{
    public class StackQueryBuilder : IElasticQueryBuilder
    {
        private static readonly Field StackIdField = nameof(IOwnedByStack.StackId).ToLowerUnderscoredWords();
        private static readonly Field SignatureHashField = nameof(Stack.SignatureHash).ToLowerUnderscoredWords();

        public Task BuildAsync<T>(QueryBuilderContext<T> ctx) where T : class, new()
        {
            var stackIds = ctx.Source.GetStacks();
            if (stackIds.Count == 1)
                ctx.Filter &= new TermQuery { Field = StackIdField, Value = stackIds.Single() };
            else if (stackIds.Count > 1)
                ctx.Filter &= new TermsQuery { Field = StackIdField, Terms = new TermsQueryField(stackIds.Select(FieldValueHelper.ToFieldValue).ToList()) };

            var excludedStackIds = ctx.Source.GetExcludedStacks();
            if (excludedStackIds.Count == 1)
                ctx.Filter &= new BoolQuery { MustNot = [new TermQuery { Field = StackIdField, Value = excludedStackIds.Single() }] };
            else if (excludedStackIds.Count > 1)
                ctx.Filter &= new BoolQuery { MustNot = [new TermsQuery { Field = StackIdField, Terms = new TermsQueryField(excludedStackIds.Select(FieldValueHelper.ToFieldValue).ToList()) }] };

            var signatureHashes = ctx.Source.GetSignatureHashes();
            if (signatureHashes.Count == 1)
                ctx.Filter &= new TermQuery { Field = SignatureHashField, Value = signatureHashes.Single() };
            else if (signatureHashes.Count > 1)
                ctx.Filter &= new TermsQuery { Field = SignatureHashField, Terms = new TermsQueryField(signatureHashes.Select(FieldValueHelper.ToFieldValue).ToList()) };

            return Task.CompletedTask;
        }
    }
}
