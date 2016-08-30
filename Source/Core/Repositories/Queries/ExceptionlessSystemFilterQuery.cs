using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Elasticsearch.Queries.Options;
using Foundatio.Repositories.Queries;
using Nest;

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
        public bool IsGlobalAdmin { get; set; }
    }

    public interface IExceptionlessSystemFilterQuery : IRepositoryQuery {
        IReadOnlyCollection<Organization> Organizations { get; }
        IReadOnlyCollection<Project> Projects { get; }
        Stack Stack { get; }
        bool UsesPremiumFeatures { get; set; }
        bool IsGlobalAdmin { get; set; }
    }

    public class SystemFilterQueryBuilder : IElasticQueryBuilder {
        public void Build<T>(QueryBuilderContext<T> ctx) where T : class, new() {
            var sfq = ctx.GetSourceAs<IExceptionlessSystemFilterQuery>();
            if (sfq == null)
                return;

            //    if (HasOrganizationOrProjectOrStackFilter(filter) && Request.IsGlobalAdmin())
            //        return null;

            var allowedOrganizations = sfq.Organizations.Where(o => o.HasPremiumFeatures || (!o.HasPremiumFeatures && !sfq.UsesPremiumFeatures)).ToList();
            if (allowedOrganizations.Count == 0) {
                ctx.Filter &= Filter<T>.Term("organization", "none");
                return;
            }

            string field = GetDateField(ctx.GetOptionsAs<IElasticQueryOptions>());
            
            if (sfq.Stack != null) {
                var organization = sfq.Organizations.Single(o => o.Id == sfq.Stack.OrganizationId);
                if (organization.RetentionDays > 0)
                    ctx.Filter &= (Filter<T>.Term("stack", sfq.Stack.Id) && GetRetentionFilter<T>(field, organization.RetentionDays));
                else
                    ctx.Filter &= Filter<T>.Term("stack", sfq.Stack.Id);

                return;
            }

            var container = new FilterContainer();
            if (sfq.Projects != null) {
                foreach (var project in sfq.Projects) {
                    var organization = sfq.Organizations.Single(o => o.Id == project.OrganizationId);
                    if (organization.RetentionDays > 0)
                        container |= (Filter<T>.Term("project", project.Id) && GetRetentionFilter<T>(field, organization.RetentionDays));
                    else
                        container |= Filter<T>.Term("project", project.Id);
                }

                ctx.Filter &= container;
                return;
            }

            if (sfq.Organizations != null) {
                foreach (var organization in sfq.Organizations) {
                    if (organization.RetentionDays > 0)
                        container |= (Filter<T>.Term("organization", organization.Id) && GetRetentionFilter<T>(field, organization.RetentionDays));
                    else
                        container |= Filter<T>.Term("organization", organization.Id);
                }

                ctx.Filter &= container;
            }
        }

        private static FilterContainer GetRetentionFilter<T>(string field, int retentionDays) where T : class, new() {
            return Filter<T>.Range(r => r.OnField(field).GreaterOrEquals($"now/d-{retentionDays}d").LowerOrEquals("now/d+1d"));
        }

        private string GetDateField(IElasticQueryOptions options) {
            if (options != null && options.IndexType.GetType() == typeof(StackIndexType))
                return StackIndexType.Fields.LastOccurrence;

            return EventIndexType.Fields.Date;
        }
        
        //private bool HasOrganizationOrProjectOrStackFilter(string filter) {
        //    if (String.IsNullOrEmpty(filter))
        //        return false;

        //    return filter.Contains("organization:") || filter.Contains("project:") || filter.Contains("stack:");
        //}
    }
}