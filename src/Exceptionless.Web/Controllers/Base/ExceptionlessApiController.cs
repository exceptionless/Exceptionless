using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Web.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Utility;
using Exceptionless.Web.Utility.Results;
using Foundatio.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Controllers {
    [Produces("application/json")]
    [ApiController]
    public abstract class ExceptionlessApiController : Controller {
        public const string API_PREFIX = "api/v2";
        protected const int DEFAULT_LIMIT = 10;
        protected const int MAXIMUM_LIMIT = 100;
        protected const int MAXIMUM_SKIP = 1000;

        public ExceptionlessApiController() {
            AllowedDateFields = new List<string>();
        }

        protected TimeSpan GetOffset(string offset) {
            if (!String.IsNullOrEmpty(offset) && TimeUnit.TryParse(offset, out var value) && value.HasValue)
                return value.Value;

            return TimeSpan.Zero;
        }

        protected ICollection<string> AllowedDateFields { get; private set; }
        protected string DefaultDateField { get; set; } = "created_utc";

        protected virtual TimeInfo GetTimeInfo(string time, string offset, DateTime? minimumUtcStartDate = null) {
            string field = DefaultDateField;
            if (!String.IsNullOrEmpty(time) && time.Contains("|")) {
                string[] parts = time.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                field = parts.Length > 0 && AllowedDateFields.Contains(parts[0]) ? parts[0] : DefaultDateField;
                time = parts.Length > 1 ? parts[1] : null;
            }

            var utcOffset = GetOffset(offset);

            // range parsing needs to be based on the user's local time.
            var range = DateTimeRange.Parse(time, Foundatio.Utility.SystemClock.OffsetUtcNow.ToOffset(utcOffset));
            var timeInfo = new TimeInfo { Field = field, Offset = utcOffset, Range = range };
            if (minimumUtcStartDate.HasValue)
                timeInfo.ApplyMinimumUtcStartDate(minimumUtcStartDate.Value);

            timeInfo.AdjustEndTimeIfMaxValue();
            return timeInfo;
        }

        protected int GetLimit(int limit, int maximumLimit = MAXIMUM_LIMIT) {
            if (maximumLimit < MAXIMUM_LIMIT)
                throw new ArgumentOutOfRangeException(nameof(maximumLimit));
                
            if (limit < 1)
                limit = DEFAULT_LIMIT;
            else if (limit > maximumLimit)
                limit = maximumLimit;

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

        protected bool CanAccessOrganization(string organizationId) {
            return Request.CanAccessOrganization(organizationId);
        }

        protected bool IsInOrganization(string organizationId) {
            if (String.IsNullOrEmpty(organizationId))
                return false;

            return Request.IsInOrganization(organizationId);
        }

        protected ICollection<string> GetAssociatedOrganizationIds() {
            return Request.GetAssociatedOrganizationIds();
        }

        private static readonly IReadOnlyCollection<Organization> EmptyOrganizations = new List<Organization>(0).AsReadOnly();
        protected async Task<IReadOnlyCollection<Organization>> GetSelectedOrganizationsAsync(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IStackRepository stackRepository, string filter = null) {
            var associatedOrganizationIds = GetAssociatedOrganizationIds();
            if (associatedOrganizationIds.Count == 0)
                return EmptyOrganizations;

            if (!String.IsNullOrEmpty(filter)) {
                var scope = GetFilterScopeVisitor.Run(filter);
                if (scope.IsScopable) {
                    Organization organization = null;
                    if (scope.OrganizationId != null) {
                        organization = await organizationRepository.GetByIdAsync(scope.OrganizationId, o => o.Cache());
                    } else if (scope.ProjectId != null) {
                        var project = await projectRepository.GetByIdAsync(scope.ProjectId, o => o.Cache());
                        if (project != null)
                            organization = await organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache());
                    } else if (scope.StackId != null) {
                        var stack = await stackRepository.GetByIdAsync(scope.StackId, o => o.Cache());
                        if (stack != null)
                            organization = await organizationRepository.GetByIdAsync(stack.OrganizationId, o => o.Cache());
                    }

                    if (organization != null) {
                        if (associatedOrganizationIds.Contains(organization.Id) || Request.IsGlobalAdmin())
                            return new[] { organization }.ToList().AsReadOnly();

                        return EmptyOrganizations;
                    }
                }
            }

            var organizations = await organizationRepository.GetByIdsAsync(associatedOrganizationIds.ToArray(), o => o.Cache());
            return organizations.ToList().AsReadOnly();
        }

        protected bool ShouldApplySystemFilter(AppFilter sf, string filter) {
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

        protected ObjectResult Permission(PermissionResult permission) {
            return StatusCode(permission.StatusCode, new MessageContent(permission.Id, permission.Message));
        }

        protected ObjectResult StatusCodeWithMessage(int statusCode, string message, string reason = null) {
            return StatusCode(statusCode, new MessageContent(message, reason));
        }

        protected ActionResult<WorkInProgressResult> WorkInProgress(IEnumerable<string> workers) {
            return StatusCode(StatusCodes.Status202Accepted, new WorkInProgressResult(workers));
            }

        protected ObjectResult BadRequest(ModelActionResults results) {
            return StatusCode(StatusCodes.Status400BadRequest, results);
        }

        protected ObjectResult PlanLimitReached(string message) {
            return StatusCode(StatusCodes.Status426UpgradeRequired, new MessageContent(message));
        }

        protected ObjectResult NotImplemented(string message) {
            return StatusCode(StatusCodes.Status501NotImplemented, new MessageContent(message));
        }

        protected OkWithHeadersContentResult<T> OkWithLinks<T>(T content, string link) {
            return OkWithLinks(content, new[] { link });
        }

        protected OkWithHeadersContentResult<T> OkWithLinks<T>(T content, string[] links) {
            var headers = new HeaderDictionary();
            string[] linksToAdd = links.Where(l => l != null).ToArray();
            if (linksToAdd.Length > 0)
                headers.Add("Link", linksToAdd);

            return new OkWithHeadersContentResult<T>(content, headers);
        }

        protected OkWithResourceLinks<TEntity> OkWithResourceLinks<TEntity>(IEnumerable<TEntity> content, bool hasMore, Func<TEntity, string> pagePropertyAccessor = null, IHeaderDictionary headers = null, bool isDescending = false) where TEntity : class {
            return new OkWithResourceLinks<TEntity>(content, hasMore, null, pagePropertyAccessor, headers, isDescending);
        }

        protected OkWithResourceLinks<TEntity> OkWithResourceLinks<TEntity>(IEnumerable<TEntity> content, bool hasMore, int page, long? total = null, IHeaderDictionary headers = null) where TEntity : class {
            return new OkWithResourceLinks<TEntity>(content, hasMore, page, total, headers: headers);
        }

        protected string GetResourceLink(string url, string type) {
            return url != null ? $"<{url}>; rel=\"{type}\"" : null;
        }

        protected bool NextPageExceedsSkipLimit(int page, int limit) {
            return (page + 1) * limit >= MAXIMUM_SKIP;
        }
    }
}