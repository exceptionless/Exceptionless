using System.Diagnostics.CodeAnalysis;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Utility;
using Exceptionless.Web.Utility.Results;
using Foundatio.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Net.Http.Headers;

namespace Exceptionless.Web.Controllers;

[Produces("application/json", "application/problem+json")]
[ApiController]
public abstract class ExceptionlessApiController : Controller
{
    public const string API_PREFIX = "api/v2";
    protected const int DEFAULT_LIMIT = 10;
    protected const int MAXIMUM_LIMIT = 100;
    protected const int MAXIMUM_SKIP = 1000;
    protected static readonly char[] TIME_PARTS = ['|'];
    protected TimeProvider _timeProvider;

    protected ExceptionlessApiController(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    protected TimeSpan GetOffset(string? offset)
    {
        if (!String.IsNullOrEmpty(offset) && TimeUnit.TryParse(offset, out var value) && value.HasValue)
            return value.Value;

        return TimeSpan.Zero;
    }

    protected ICollection<string> AllowedDateFields { get; private set; } = new List<string>();
    protected string DefaultDateField { get; set; } = "created_utc";

    protected virtual TimeInfo GetTimeInfo(string? time, string? offset, DateTime? minimumUtcStartDate = null)
    {
        string field = DefaultDateField;
        if (!String.IsNullOrEmpty(time) && time.Contains('|'))
        {
            string[] parts = time.Split(TIME_PARTS, StringSplitOptions.RemoveEmptyEntries);
            field = parts.Length > 0 && AllowedDateFields.Contains(parts[0]) ? parts[0] : DefaultDateField;
            time = parts.Length > 1 ? parts[1] : null;
        }

        var utcOffset = GetOffset(offset);

        // range parsing needs to be based on the user's local time.
        var range = DateTimeRange.Parse(time, _timeProvider.GetUtcNow().ToOffset(utcOffset));
        var timeInfo = new TimeInfo { Field = field, Offset = utcOffset, Range = range };
        if (minimumUtcStartDate.HasValue)
            timeInfo.ApplyMinimumUtcStartDate(minimumUtcStartDate.Value);

        timeInfo.AdjustEndTimeIfMaxValue(_timeProvider);
        return timeInfo;
    }

    protected int GetLimit(int limit, int maximumLimit = MAXIMUM_LIMIT)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumLimit, MAXIMUM_LIMIT);

        if (limit < 1)
            limit = DEFAULT_LIMIT;
        else if (limit > maximumLimit)
            limit = maximumLimit;

        return limit;
    }

    protected int GetPage(int page)
    {
        if (page < 1)
            page = 1;

        return page;
    }

    protected int GetSkip(int currentPage, int limit)
    {
        if (currentPage < 1)
            currentPage = 1;

        int skip = (currentPage - 1) * limit;
        if (skip < 0)
            skip = 0;

        return skip;
    }

    /// <summary>
    /// This call will throw an exception if the user is a token auth type.
    /// This is less than ideal, and we should refactor this to be a nullable user.
    /// NOTE: The only endpoints that allow token auth types is
    ///     - post event
    ///     - post user event description
    ///     - post session heartbeat
    ///     - post session end
    ///     - project config
    /// </summary>
    protected virtual User CurrentUser => Request.GetUser();

    protected bool CanAccessOrganization(string organizationId)
    {
        return Request.CanAccessOrganization(organizationId);
    }

    protected bool IsInOrganization([NotNullWhen(true)] string? organizationId)
    {
        if (String.IsNullOrEmpty(organizationId))
            return false;

        return Request.IsInOrganization(organizationId);
    }

    protected ICollection<string> GetAssociatedOrganizationIds()
    {
        return Request.GetAssociatedOrganizationIds();
    }

    private static readonly IReadOnlyCollection<Organization> EmptyOrganizations = new List<Organization>(0).AsReadOnly();
    protected async Task<IReadOnlyCollection<Organization>> GetSelectedOrganizationsAsync(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IStackRepository stackRepository, string? filter = null)
    {
        var associatedOrganizationIds = GetAssociatedOrganizationIds();
        if (associatedOrganizationIds.Count == 0)
            return EmptyOrganizations;

        if (!String.IsNullOrEmpty(filter))
        {
            var scope = GetFilterScopeVisitor.Run(filter);
            if (scope.IsScopable)
            {
                Organization? organization = null;
                if (scope.OrganizationId is not null)
                {
                    organization = await organizationRepository.GetByIdAsync(scope.OrganizationId, o => o.Cache());
                }
                else if (scope.ProjectId is not null)
                {
                    var project = await projectRepository.GetByIdAsync(scope.ProjectId, o => o.Cache());
                    if (project is not null)
                        organization = await organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache());
                }
                else if (scope.StackId is not null)
                {
                    var stack = await stackRepository.GetByIdAsync(scope.StackId, o => o.Cache());
                    if (stack is not null)
                        organization = await organizationRepository.GetByIdAsync(stack.OrganizationId, o => o.Cache());
                }

                if (organization is not null)
                {
                    if (associatedOrganizationIds.Contains(organization.Id) || Request.IsGlobalAdmin())
                        return new[] { organization }.ToList().AsReadOnly();

                    return EmptyOrganizations;
                }
            }
        }

        var organizations = await organizationRepository.GetByIdsAsync(associatedOrganizationIds.ToArray(), o => o.Cache());
        return organizations.ToList().AsReadOnly();
    }

    protected bool ShouldApplySystemFilter(AppFilter sf, string? filter)
    {
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
        var scope = GetFilterScopeVisitor.Run(filter);
        bool hasOrganizationOrProjectOrStackFilter = !String.IsNullOrEmpty(scope.OrganizationId) || !String.IsNullOrEmpty(scope.ProjectId) || !String.IsNullOrEmpty(scope.StackId);
        return !hasOrganizationOrProjectOrStackFilter;
    }

    protected ObjectResult Permission(PermissionResult permission)
    {
        if (permission.StatusCode is StatusCodes.Status422UnprocessableEntity)
            return (ObjectResult)ValidationProblem(ModelState);

        if (String.IsNullOrEmpty(permission.Message))
            return Problem(statusCode: permission.StatusCode);

        return Problem(statusCode: permission.StatusCode, title: permission.Message);
    }

    protected ActionResult<WorkInProgressResult> WorkInProgress(IEnumerable<string> workers)
    {
        return StatusCode(StatusCodes.Status202Accepted, new WorkInProgressResult(workers));
    }

    protected ObjectResult BadRequest(ModelActionResults results)
    {
        return StatusCode(StatusCodes.Status400BadRequest, results);
    }

    protected StatusCodeResult Forbidden()
    {
        return StatusCode(StatusCodes.Status403Forbidden);
    }

    protected ObjectResult Forbidden(string message)
    {
        return Problem(statusCode: StatusCodes.Status403Forbidden, title: message);
    }

    protected ObjectResult PlanLimitReached(string message)
    {
        return Problem(statusCode: StatusCodes.Status426UpgradeRequired, title: message);
    }

    protected ObjectResult TooManyRequests(string message)
    {
        return Problem(statusCode: StatusCodes.Status429TooManyRequests, title: message);
    }

    protected ObjectResult NotImplemented(string message)
    {
        return Problem(statusCode: StatusCodes.Status501NotImplemented, title: message);
    }

    protected OkWithHeadersContentResult<T> OkWithLinks<T>(T content, string link)
    {
        return OkWithLinks(content, [link]);
    }

    protected OkWithHeadersContentResult<T> OkWithLinks<T>(T content, string?[] links)
    {
        var headers = new HeaderDictionary();
        string[] linksToAdd = links.Where(l => !String.IsNullOrEmpty(l)).ToArray()!;
        if (linksToAdd.Length > 0)
            headers.Add(HeaderNames.Link, linksToAdd);

        return new OkWithHeadersContentResult<T>(content, headers);
    }

    protected OkWithResourceLinks<TEntity> OkWithResourceLinks<TEntity>(ICollection<TEntity> content, bool hasMore, int? page = null, long? total = null, string? before = null, string? after = null) where TEntity : class
    {
        return new OkWithResourceLinks<TEntity>(content, hasMore, page, total, before, after);
    }

    protected string? GetResourceLink(string? url, string type)
    {
        return url is not null ? $"<{url}>; rel=\"{type}\"" : null;
    }

    protected bool NextPageExceedsSkipLimit(int? page, int limit)
    {
        if (page is null)
            return false;

        return (page + 1) * limit >= MAXIMUM_SKIP;
    }

    // We need to override this to ensure Validation Problems return a 422 status code.
    public override ActionResult ValidationProblem(string? detail = null, string? instance = null, int? statusCode = null,
        string? title = null, string? type = null, ModelStateDictionary? modelStateDictionary = null,
        IDictionary<string, object?>? extensions = null) =>
        base.ValidationProblem(detail, instance, statusCode ?? 422, title, type, modelStateDictionary, extensions);
}
