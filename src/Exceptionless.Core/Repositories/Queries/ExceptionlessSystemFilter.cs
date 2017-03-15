using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Options;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.DateTimeExtensions;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Options;
using Nest;
using Foundatio.Utility;

namespace Exceptionless.Core.Repositories {
    public static class ExceptionlessSystemFilterQueryExtensions {
        internal const string SystemFilterKey = "@ExceptionlessSystemFilter";

        public static T SystemFilter<T>(this T query, ExceptionlessSystemFilter filter) where T : IRepositoryQuery {
            if (filter != null)
                return query.BuildOption(SystemFilterKey, filter);

            return query;
        }
    }
}

namespace Exceptionless.Core.Repositories.Options {
    public static class ReadExceptionlessSystemFilterQueryExtensions {
        public static ExceptionlessSystemFilter GetSystemFilter(this IRepositoryQuery query) {
            return query.SafeGetOption<ExceptionlessSystemFilter>(ExceptionlessSystemFilterQueryExtensions.SystemFilterKey);
        }
    }
}

namespace Exceptionless.Core.Repositories.Queries {
    public class ExceptionlessSystemFilter {
        public ExceptionlessSystemFilter(Organization organization) : this(new List<Organization> { organization }) {
            if (organization == null)
                throw new ArgumentNullException(nameof(organization));
        }

        public ExceptionlessSystemFilter(IReadOnlyCollection<Organization> organizations) {
            Organizations = organizations ?? throw new ArgumentNullException(nameof(organizations));
        }

        public ExceptionlessSystemFilter(Project project, Organization organization) : this(new List<Project> { project }, new List<Organization> { organization }) {
            if (organization == null)
                throw new ArgumentNullException(nameof(organization));

            if (project == null)
                throw new ArgumentNullException(nameof(project));
        }

        public ExceptionlessSystemFilter(IReadOnlyCollection<Project> projects, IReadOnlyCollection<Organization> organizations) : this(organizations) {
            Projects = projects ?? throw new ArgumentNullException(nameof(projects));
        }

        public ExceptionlessSystemFilter(Stack stack, Organization organization) : this(new List<Organization> { organization }) {
            Stack = stack ?? throw new ArgumentNullException(nameof(stack));
        }

        public IReadOnlyCollection<Organization> Organizations { get; }
        public IReadOnlyCollection<Project> Projects { get; }
        public Stack Stack { get; }
        public bool UsesPremiumFeatures { get; set; }
        public bool IsUserOrganizationsFilter { get; set; }
    }

    public class ExceptionlessSystemFilterQueryBuilder : IElasticQueryBuilder {
        private readonly string _organizationIdFieldName;
        private readonly string _projectIdFieldName;
        private readonly string _stackIdFieldName;
        private readonly string _stackLastOccurrenceFieldName;
        private readonly string _eventDateFieldName;

        public ExceptionlessSystemFilterQueryBuilder() {
            _organizationIdFieldName = nameof(IOwnedByOrganization.OrganizationId).ToLowerUnderscoredWords();
            _projectIdFieldName = nameof(IOwnedByProject.ProjectId).ToLowerUnderscoredWords();
            _stackIdFieldName = nameof(IOwnedByStack.StackId).ToLowerUnderscoredWords();
            _stackLastOccurrenceFieldName = nameof(Stack.LastOccurrence).ToLowerUnderscoredWords();
            _eventDateFieldName = nameof(Event.Date).ToLowerUnderscoredWords();
        }

        public Task BuildAsync<T>(QueryBuilderContext<T> ctx) where T : class, new() {
            if (ctx.Type != ContextType.Default)
                return Task.CompletedTask;

            var sfq = ctx.Source.GetSystemFilter();
            if (sfq == null)
                return Task.CompletedTask;

            var allowedOrganizations = sfq.Organizations.Where(o => o.HasPremiumFeatures || (!o.HasPremiumFeatures && !sfq.UsesPremiumFeatures)).ToList();
            if (allowedOrganizations.Count == 0) {
                ctx.Filter &= Query<T>.Term(_organizationIdFieldName, "none");
                return Task.CompletedTask;
            }

            string field = GetDateField(ctx.Options.GetElasticTypeSettings());
            if (sfq.Stack != null) {
                var organization = allowedOrganizations.SingleOrDefault(o => o.Id == sfq.Stack.OrganizationId);
                if (organization != null)
                    ctx.Filter &= (Query<T>.Term(_stackIdFieldName, sfq.Stack.Id) && GetRetentionFilter<T>(field, organization, sfq.Stack.FirstOccurrence));
                else
                    ctx.Filter &= Query<T>.Term(_stackIdFieldName, "none");

                return Task.CompletedTask;
            }

            QueryContainer container = null;
            if (sfq.Projects?.Count > 0) {
                var allowedProjects = sfq.Projects.ToDictionary(p => p, p => allowedOrganizations.SingleOrDefault(o => o.Id == p.OrganizationId)).Where(kvp => kvp.Value != null).ToList();
                if (allowedProjects.Count > 0) {
                    foreach (var project in allowedProjects)
                        container |= (Query<T>.Term(_projectIdFieldName, project.Key.Id) && GetRetentionFilter<T>(field, project.Value, project.Key.CreatedUtc.SafeSubtract(TimeSpan.FromDays(3))));

                    ctx.Filter &= container;
                    return Task.CompletedTask;
                }

                ctx.Filter &= (Query<T>.Term(_projectIdFieldName, "none"));
                return Task.CompletedTask;
            }

            foreach (var organization in allowedOrganizations)
                container |= (Query<T>.Term(_organizationIdFieldName, organization.Id) && GetRetentionFilter<T>(field, organization));

            ctx.Filter &= container;
            return Task.CompletedTask;
        }

        private static QueryContainer GetRetentionFilter<T>(string field, Organization organization, DateTime? oldestPossibleEventAge = null) where T : class, new() {
            var retentionDate = organization.GetRetentionUtcCutoff(oldestPossibleEventAge);
            double retentionDays = Math.Max(Math.Round(Math.Abs(SystemClock.UtcNow.Subtract(retentionDate).TotalDays), MidpointRounding.AwayFromZero), 1);
            return Query<T>.DateRange(r => r.Field(field).GreaterThanOrEquals($"now/d-{(int)retentionDays}d").LessThanOrEquals("now/d+1d"));
        }

        private string GetDateField(ElasticTypeSettings settings) {
            if (settings != null && settings.IndexType.GetType() == typeof(StackIndexType))
                return _stackLastOccurrenceFieldName;

            return _eventDateFieldName;
        }
    }
}