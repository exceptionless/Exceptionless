using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Results;
using Exceptionless.Api.Extensions;
using Exceptionless.Api.Security;
using Exceptionless.Api.Utility;
using Exceptionless.Api.Utility.Results;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.DateTimeExtensions;
using Foundatio.Utility;
using Foundatio.Parsers.LuceneQueries;
using Foundatio.Parsers.LuceneQueries.Visitors;
using Foundatio.Parsers.LuceneQueries.Nodes;

namespace Exceptionless.Api.Controllers {
    [RequireHttpsExceptLocal]
    public abstract class ExceptionlessApiController : ApiController {
        public const string API_PREFIX = "api/v2";
        protected const int DEFAULT_LIMIT = 10;
        protected const int MAXIMUM_LIMIT = 100;
        protected const int MAXIMUM_SKIP = 1000;

        public ExceptionlessApiController() {
            AllowedDateFields = new List<string>();
        }

        protected TimeSpan GetOffset(string offset) {
            TimeSpan? value;
            if (!String.IsNullOrEmpty(offset) && TimeUnit.TryParse(offset, out value) && value.HasValue)
                return value.Value;

            return TimeSpan.Zero;
        }

        protected ICollection<string> AllowedDateFields { get; private set; }
        protected string DefaultDateField { get; set; } = "created_utc";

        protected virtual TimeInfo GetTimeInfo(string time, string offset, DateTime? minimumUtcStartDate = null) {
            string field = DefaultDateField;
            if (!String.IsNullOrEmpty(time) && time.Contains("|")) {
                var parts = time.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                field = parts.Length > 0 && AllowedDateFields.Contains(parts[0]) ? parts[0] : DefaultDateField;
                time = parts.Length > 1 ? parts[1] : null;
            }

            var utcOffset = GetOffset(offset);

            // range parsing needs to be based on the user's local time.
            var range = DateTimeRange.Parse(time, SystemClock.OffsetUtcNow.ToOffset(utcOffset));
            var timeInfo = new TimeInfo { Field = field, Offset = utcOffset, Range = range };
            if (minimumUtcStartDate.HasValue)
                timeInfo.ApplyMinimumUtcStartDate(minimumUtcStartDate.Value);

            timeInfo.AdjustEndTimeIfMaxValue();
            return timeInfo;
        }

        protected int GetLimit(int limit) {
            if (limit < 1)
                limit = DEFAULT_LIMIT;
            else if (limit > MAXIMUM_LIMIT)
                limit = MAXIMUM_LIMIT;

            return limit;
        }

        protected int GetPage(int page) {
            if (page < 1)
                page = 1;

            return page;
        }

        protected int GetSkip(int currentPage, int limit) {
            if (currentPage < 1)
                currentPage = 1;

            int skip = (currentPage - 1) * limit;
            if (skip < 0)
                skip = 0;

            return skip;
        }

        protected User CurrentUser => Request.GetUser();

        public bool CanAccessOrganization(string organizationId) {
            return Request.CanAccessOrganization(organizationId);
        }

        public bool IsInOrganization(string organizationId) {
            if (String.IsNullOrEmpty(organizationId))
                return false;

            return Request.IsInOrganization(organizationId);
        }

        public ICollection<string> GetAssociatedOrganizationIds() {
            return Request.GetAssociatedOrganizationIds();
        }

        private static readonly IReadOnlyCollection<Organization> EmptyOrganizations = new List<Organization>(0).AsReadOnly();
        public async Task<IReadOnlyCollection<Organization>> GetSelectedOrganizationsAsync(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IStackRepository stackRepository, string filter = null) {
            if (!String.IsNullOrEmpty(filter)) {
                var scope = GetFilterScopeVisitor.Run(filter);
                if (scope.IsScopable) {
                    Organization org = null;
                    if (scope.OrganizationId != null) {
                        org = await organizationRepository.GetByIdAsync(scope.OrganizationId, true);
                    } else if (scope.ProjectId != null) {
                        var project = await projectRepository.GetByIdAsync(scope.ProjectId, true);
                        if (project != null)
                            org = await organizationRepository.GetByIdAsync(project.OrganizationId, true);
                    } else if (scope.StackId != null) {
                        var stack = await stackRepository.GetByIdAsync(scope.StackId, true);
                        if (stack != null)
                            org = await organizationRepository.GetByIdAsync(stack.OrganizationId, true);
                    }

                    if (org != null)
                        return new[] { org }.ToList().AsReadOnly();
                }
            }

            var ids = GetAssociatedOrganizationIds();
            if (ids.Count == 0)
                return EmptyOrganizations;

            var organizations = await organizationRepository.GetByIdsAsync(ids, true);
            return organizations.ToList().AsReadOnly();
        }

        protected bool ShouldApplySystemFilter(IExceptionlessSystemFilterQuery sf, string filter) {
            // Apply filter to non admin user.
            if (!Request.IsGlobalAdmin())
                return true;

            // Apply filter as it's scoped via a controller action.
            if (!sf.IsUserOrganizationsFilter)
                return true;

            // Empty user filter
            if (String.IsNullOrEmpty(filter))
                return true;

            // Used for impersonating a user. Only skip the filter if it contains an org, project or stack.
            bool hasOrganizationOrProjectOrStackFilter = filter.Contains("organization:") || filter.Contains("project:") || filter.Contains("stack:");
            return !hasOrganizationOrProjectOrStackFilter;
        }

        protected StatusCodeActionResult StatusCodeWithMessage(HttpStatusCode statusCode, string message, string reason = null) {
            return new StatusCodeActionResult(statusCode, Request, message, reason);
        }

        protected IHttpActionResult WorkInProgress(IEnumerable<string> workers) {
            return new NegotiatedContentResult<WorkInProgressResult>(HttpStatusCode.Accepted, new WorkInProgressResult(workers), this);
        }

        protected IHttpActionResult BadRequest(ModelActionResults results) {
            return new NegotiatedContentResult<ModelActionResults>(HttpStatusCode.BadRequest, results, this);
        }

        public PermissionActionResult Permission(PermissionResult permission) {
            return new PermissionActionResult(permission, Request);
        }

        public PlanLimitReachedActionResult PlanLimitReached(string message) {
            return new PlanLimitReachedActionResult(message, Request);
        }

        public NotImplementedActionResult NotImplemented(string message) {
            return new NotImplementedActionResult(message, Request);
        }

        public OkWithHeadersContentResult<T> OkWithLinks<T>(T content, params string[] links) {
            return new OkWithHeadersContentResult<T>(content, this, links.Where(l => l != null).Select(l => new KeyValuePair<string, IEnumerable<string>>("Link", new[] { l })));
        }

        public OkWithHeadersContentResult<T> OkWithHeaders<T>(T content, params Tuple<string, string>[] headers) {
            return new OkWithHeadersContentResult<T>(content, this, headers.Where(h => h != null).Select(h => new KeyValuePair<string, IEnumerable<string>>(h.Item1, new[] { h.Item2 })));
        }

        public OkWithHeadersContentResult<T> OkWithHeaders<T>(T content, params Tuple<string, string[]>[] headers) {
            return new OkWithHeadersContentResult<T>(content, this, headers.Select(h => new KeyValuePair<string, IEnumerable<string>>(h.Item1, h.Item2)));
        }

        public OkWithHeadersContentResult<T> OkWithHeaders<T>(T content, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers) {
            return new OkWithHeadersContentResult<T>(content, this, headers);
        }

        public OkWithResourceLinks<TEntity> OkWithResourceLinks<TEntity>(IEnumerable<TEntity> content, bool hasMore, Func<TEntity, string> pagePropertyAccessor = null, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers = null, bool isDescending = false) where TEntity : class {
            return new OkWithResourceLinks<TEntity>(content, this, hasMore, null, pagePropertyAccessor, headers, isDescending);
        }

        public OkWithResourceLinks<TEntity> OkWithResourceLinks<TEntity>(IEnumerable<TEntity> content, bool hasMore, int page, long? total = null, IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers = null) where TEntity : class {
            return new OkWithResourceLinks<TEntity>(content, this, hasMore, page, total);
        }

        protected Dictionary<string, IEnumerable<string>> GetLimitedByPlanHeader(long totalLimitedByPlan) {
            var headers = new Dictionary<string, IEnumerable<string>>();
            if (totalLimitedByPlan > 0)
                headers.Add(ExceptionlessHeaders.LimitedByPlan, new[] { totalLimitedByPlan.ToString() });
            return headers;
        }

        protected string GetResourceLink(string url, string type) {
            return url != null ? $"<{url}>; rel=\"{type}\"" : null;
        }

        protected bool NextPageExceedsSkipLimit(int page, int limit) {
            return (page + 1) * limit >= MAXIMUM_SKIP;
        }

        public string GetSystemFilter(bool filterUsesPremiumFeatures, bool hasOrganizationFilter) {
            if (hasOrganizationFilter && Request.IsGlobalAdmin())
                return null;

            return null;
        }
    }

    public class GetFilterScopeVisitor : QueryNodeVisitorWithResultBase<FilterScope> {
        private readonly FilterScope _scope = new FilterScope();
        private static LuceneQueryParser _parser = new LuceneQueryParser();

        public override void Visit(TermNode node, IQueryVisitorContext context) {
            if (String.IsNullOrEmpty(node.Field) || !_scope.IsScopable)
                return;

            if (node.Field.Equals("organization")) {
                if (!_scope.HasScope)
                    _scope.OrganizationId = node.UnescapedTerm;
                else // found dupe, mark filter as not scopable
                    _scope.IsScopable = false;
            } else if (node.Field.Equals("project")) {
                if (!_scope.HasScope)
                    _scope.ProjectId = node.UnescapedTerm;
                else // found dupe, mark filter as not scopable
                    _scope.IsScopable = false;
            } else if (node.Field.Equals("stack")) {
                if (!_scope.HasScope)
                    _scope.StackId = node.UnescapedTerm;
                else // found dupe, mark filter as not scopable
                    _scope.IsScopable = false;
            }
        }

        public override Task<FilterScope> AcceptAsync(IQueryNode node, IQueryVisitorContext context) {
            node.AcceptAsync(this, context);
            return Task.FromResult(_scope);
        }

        public static FilterScope Run(string filter) {
            var node = _parser.Parse(filter);
            return new GetFilterScopeVisitor().AcceptAsync(node, null).GetAwaiter().GetResult();
        }
    }

    public class FilterScope {
        public string OrganizationId { get; set; }
        public string ProjectId { get; set; }
        public string StackId { get; set; }
        public bool IsScopable { get; set; } = true;
        public bool HasScope => OrganizationId != null || ProjectId != null || StackId != null;
    }
}
