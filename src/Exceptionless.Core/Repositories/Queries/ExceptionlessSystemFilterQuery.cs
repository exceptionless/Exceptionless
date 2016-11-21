using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Elasticsearch.Queries.Options;
using Foundatio.Repositories.Queries;
using Nest;
using System.Threading.Tasks;

namespace Exceptionless.Core.Repositories.Queries {
    public class ExceptionlessSystemFilterQuery : IExceptionlessSystemFilterQuery {
        public ExceptionlessSystemFilterQuery(Organization organization) : this(new List<Organization> { organization }) {
            if (organization == null)
                throw new ArgumentNullException(nameof(organization));
        }

        public ExceptionlessSystemFilterQuery(IReadOnlyCollection<Organization> organizations) {
            if (organizations == null)
                throw new ArgumentNullException(nameof(organizations));

            Organizations = organizations;
        }

        public ExceptionlessSystemFilterQuery(Project project, Organization organization) : this(new List<Project> { project }, new List<Organization> { organization }) {
            if (organization == null)
                throw new ArgumentNullException(nameof(organization));

            if (project == null)
                throw new ArgumentNullException(nameof(project));
        }

        public ExceptionlessSystemFilterQuery(IReadOnlyCollection<Project> projects, IReadOnlyCollection<Organization> organizations) : this(organizations) {
            if (projects == null)
                throw new ArgumentNullException(nameof(projects));

            Projects = projects;
        }

        public ExceptionlessSystemFilterQuery(Stack stack, Organization organization) : this(new List<Organization> { organization }) {
            if (stack == null)
                throw new ArgumentNullException(nameof(stack));

            Stack = stack;
        }

        public IReadOnlyCollection<Organization> Organizations { get; }
        public IReadOnlyCollection<Project> Projects { get; }
        public Stack Stack { get; }
        public bool UsesPremiumFeatures { get; set; }
        public bool IsUserOrganizationsFilter { get; set; }
    }

    public interface IExceptionlessSystemFilterQuery : IRepositoryQuery {
        IReadOnlyCollection<Organization> Organizations { get; }
        IReadOnlyCollection<Project> Projects { get; }
        Stack Stack { get; }
        bool UsesPremiumFeatures { get; set; }
         bool IsUserOrganizationsFilter { get; set; }
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
            var sfq = ctx.GetSourceAs<IExceptionlessSystemFilterQuery>();
            if (sfq == null)
                return Task.CompletedTask;

            var allowedOrganizations = sfq.Organizations.Where(o => o.HasPremiumFeatures || (!o.HasPremiumFeatures && !sfq.UsesPremiumFeatures)).ToList();
            if (allowedOrganizations.Count == 0) {
                ctx.Query &= Query<T>.Term(_organizationIdFieldName, "none");
                return Task.CompletedTask;
            }

            string field = GetDateField(ctx.GetOptionsAs<IElasticQueryOptions>());
            if (sfq.Stack != null) {
                var organization = sfq.Organizations.Single(o => o.Id == sfq.Stack.OrganizationId);
                ctx.Query &= (Query<T>.Term(_stackIdFieldName, sfq.Stack.Id) && GetRetentionFilter<T>(field, organization.RetentionDays));
                return Task.CompletedTask;
            }

            QueryContainer container = null;
            if (sfq.Projects?.Count > 0) {
                foreach (var project in sfq.Projects) {
                    var organization = sfq.Organizations.Single(o => o.Id == project.OrganizationId);
                    container |= (Query<T>.Term(_projectIdFieldName, project.Id) && GetRetentionFilter<T>(field, organization.RetentionDays));
                }

                ctx.Query &= container;
                return Task.CompletedTask;
            }

            if (sfq.Organizations?.Count > 0) {
                foreach (var organization in sfq.Organizations)
                    container |= (Query<T>.Term(_organizationIdFieldName, organization.Id) && GetRetentionFilter<T>(field, organization.RetentionDays));

                ctx.Query &= container;
            }

            return Task.CompletedTask;
        }

        private static QueryContainer GetRetentionFilter<T>(string field, int retentionDays) where T : class, new() {
            if (retentionDays > 0)
                return Query<T>.DateRange(r => r.Field(field).GreaterThanOrEquals($"now/d-{retentionDays}d").LessThanOrEquals("now/d+1d"));

            return null;
        }

        private string GetDateField(IElasticQueryOptions options) {
            if (options != null && options.IndexType.GetType() == typeof(StackIndexType))
                return _stackLastOccurrenceFieldName;

            return _eventDateFieldName;
        }
    }
}