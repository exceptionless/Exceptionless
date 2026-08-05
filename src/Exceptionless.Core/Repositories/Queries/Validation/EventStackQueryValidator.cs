using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Parsers.LuceneQueries;
using Foundatio.Parsers.LuceneQueries.Visitors;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Queries.Validation;

public sealed class EventStackQueryValidator : AppQueryValidator
{
    public EventStackQueryValidator(ExceptionlessElasticConfiguration configuration, ILoggerFactory loggerFactory)
        : base(configuration.Events.QueryParser, loggerFactory) { }

    protected override QueryProcessResult ApplyQueryRules(QueryValidationResult result)
    {
        return new QueryProcessResult
        {
            IsValid = result.IsValid,
            UsesPremiumFeatures = !result.ReferencedFields.All(field =>
                PersistentEventQueryValidator.IsFreeQueryField(field)
                || StackQueryValidator.IsFreeQueryField(field))
        };
    }
}
