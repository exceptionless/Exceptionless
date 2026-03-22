using Elastic.Clients.Elasticsearch.QueryDsl;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Options;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.DateTimeExtensions;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Configuration;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Options;

namespace Exceptionless.Core.Repositories
{
    public static class AppFilterQueryExtensions
    {
        internal const string SystemFilterKey = "@AppFilter";

        public static T AppFilter<T>(this T query, AppFilter? filter) where T : IRepositoryQuery
        {
            if (filter is not null)
                return query.BuildOption(SystemFilterKey, filter);

            return query;
        }
    }
}

namespace Exceptionless.Core.Repositories.Options
{
    public static class ReadAppFilterQueryExtensions
    {
        public static bool HasAppFilter(this IRepositoryQuery query)
        {
            return query.SafeHasOption(AppFilterQueryExtensions.SystemFilterKey);
        }

        public static AppFilter? GetAppFilter(this IRepositoryQuery query)
        {
            return query.SafeGetOption<AppFilter>(AppFilterQueryExtensions.SystemFilterKey);
        }
    }
}

namespace Exceptionless.Core.Repositories.Queries
{
    public class AppFilter
    {
        public AppFilter(Organization organization) : this(new List<Organization> { organization })
        {
            ArgumentNullException.ThrowIfNull(organization);
        }

        public AppFilter(IReadOnlyCollection<Organization> organizations)
        {
            Organizations = organizations ?? throw new ArgumentNullException(nameof(organizations));
        }

        public AppFilter(Project project, Organization organization) : this(new List<Project> { project }, new List<Organization> { organization })
        {
            ArgumentNullException.ThrowIfNull(organization);
            ArgumentNullException.ThrowIfNull(project);
        }

        public AppFilter(IReadOnlyCollection<Project> projects, IReadOnlyCollection<Organization> organizations) : this(organizations)
        {
            Projects = projects ?? throw new ArgumentNullException(nameof(projects));
        }

        public AppFilter(Stack stack, Organization organization) : this(new List<Organization> { organization })
        {
            Stack = stack ?? throw new ArgumentNullException(nameof(stack));
        }

        public IReadOnlyCollection<Organization> Organizations { get; }
        public IReadOnlyCollection<Project>? Projects { get; }
        public Stack? Stack { get; }
        public bool UsesPremiumFeatures { get; set; }
        public bool IsUserOrganizationsFilter { get; set; }
    }

    public class AppFilterQueryBuilder : IElasticQueryBuilder
    {
        private readonly AppOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly string _organizationIdFieldName;
        private readonly string _projectIdFieldName;
        private readonly string _stackIdFieldName;
        private readonly string _stackLastOccurrenceFieldName;
        private readonly string _eventDateFieldName;

        public AppFilterQueryBuilder(AppOptions options, TimeProvider timeProvider)
        {
            _options = options;
            _timeProvider = timeProvider;
            _organizationIdFieldName = nameof(IOwnedByOrganization.OrganizationId).ToLowerUnderscoredWords();
            _projectIdFieldName = nameof(IOwnedByProject.ProjectId).ToLowerUnderscoredWords();
            _stackIdFieldName = nameof(IOwnedByStack.StackId).ToLowerUnderscoredWords();
            _stackLastOccurrenceFieldName = nameof(Stack.LastOccurrence).ToLowerUnderscoredWords();
            _eventDateFieldName = nameof(Event.Date).ToLowerUnderscoredWords();
        }

        public Task BuildAsync<T>(QueryBuilderContext<T> ctx) where T : class, new()
        {
            var sfq = ctx.Source.GetAppFilter();
            if (sfq is null)
                return Task.CompletedTask;

            var allowedOrganizations = sfq.Organizations.Where(o => o.HasPremiumFeatures || (!o.HasPremiumFeatures && !sfq.UsesPremiumFeatures)).ToList();
            if (allowedOrganizations.Count == 0)
            {
                ctx.Filter &= new TermQuery { Field = _organizationIdFieldName, Value = "none" };
                return Task.CompletedTask;
            }

            var index = ctx.Options.GetElasticIndex();
            bool shouldApplyRetentionFilter = ShouldApplyRetentionFilter(index, ctx);
            string? field = shouldApplyRetentionFilter ? GetDateField(index) : null;

            if (sfq.Stack is not null)
            {
                string stackIdFieldName = typeof(T) == typeof(Stack) ? "id" : _stackIdFieldName;
                var organization = allowedOrganizations.SingleOrDefault(o => o.Id == sfq.Stack.OrganizationId);
                if (organization is not null)
                {
                    if (shouldApplyRetentionFilter)
                        ctx.Filter &= new TermQuery { Field = stackIdFieldName, Value = sfq.Stack.Id } & GetRetentionFilter(field, organization, _options.MaximumRetentionDays, sfq.Stack.FirstOccurrence);
                    else
                    {
                        ctx.Filter &= new TermQuery { Field = stackIdFieldName, Value = sfq.Stack.Id };
                    }
                }
                else
                {
                    ctx.Filter &= new TermQuery { Field = stackIdFieldName, Value = "none" };
                }

                return Task.CompletedTask;
            }

            Query? container = null;
            if (sfq.Projects?.Count > 0)
            {
                var allowedProjects = sfq.Projects.ToDictionary(p => p, p => allowedOrganizations.SingleOrDefault(o => o.Id == p.OrganizationId)).Where(kvp => kvp.Value is not null).ToList();
                if (allowedProjects.Count > 0)
                {
                    foreach (var project in allowedProjects)
                    {
                        Query termQuery = new TermQuery { Field = _projectIdFieldName, Value = project.Key.Id };
                        if (shouldApplyRetentionFilter)
                            termQuery &= GetRetentionFilter(field, project.Value!, _options.MaximumRetentionDays, project.Key.CreatedUtc.SafeSubtract(TimeSpan.FromDays(3)));
                        container = container is not null ? container | termQuery : termQuery;
                    }

                    if (container is not null)
                        ctx.Filter &= container;
                    return Task.CompletedTask;
                }

                ctx.Filter &= new TermQuery { Field = _projectIdFieldName, Value = "none" };
                return Task.CompletedTask;
            }

            foreach (var organization in allowedOrganizations)
            {
                Query termQuery = new TermQuery { Field = _organizationIdFieldName, Value = organization.Id };
                if (shouldApplyRetentionFilter)
                    termQuery &= GetRetentionFilter(field, organization, _options.MaximumRetentionDays);
                container = container is not null ? container | termQuery : termQuery;
            }

            if (container is not null)
                ctx.Filter &= container;
            return Task.CompletedTask;
        }

        private Query GetRetentionFilter(string? field, Organization organization, int maximumRetentionDays, DateTime? oldestPossibleEventAge = null)
        {
            if (field is null)
                throw new ArgumentNullException(nameof(field), "Retention field not specified for this index");

            var retentionDate = organization.GetRetentionUtcCutoff(maximumRetentionDays, oldestPossibleEventAge, _timeProvider);
            double retentionDays = Math.Max(Math.Round(Math.Abs(_timeProvider.GetUtcNow().UtcDateTime.Subtract(retentionDate).TotalDays), MidpointRounding.AwayFromZero), 1);
            return new DateRangeQuery { Field = field, Gte = $"now/d-{(int)retentionDays}d", Lte = "now/d+1d" };
        }

        private static bool ShouldApplyRetentionFilter<T>(IIndex index, QueryBuilderContext<T> ctx) where T : class, new()
        {
            ArgumentNullException.ThrowIfNull(index);

            var indexType = index.GetType();
            if (indexType == typeof(StackIndex))
                return !ctx.Source.IsEventStackFilterInverted();

            if (indexType == typeof(EventIndex))
                return true;

            return false;
        }

        private string? GetDateField(IIndex index)
        {
            ArgumentNullException.ThrowIfNull(index);

            var indexType = index.GetType();
            if (indexType == typeof(StackIndex))
                return _stackLastOccurrenceFieldName;

            if (indexType == typeof(EventIndex))
                return _eventDateFieldName;

            return null;
        }
    }
}
